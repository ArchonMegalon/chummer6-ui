from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import shutil
import stat
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


scope = load(
    "preview_nightly_unsigned_scope_for_tests",
    ROOT / "scripts" / "preview_nightly_unsigned_scope.py",
)
stage = load(
    "preview_nightly_unsigned_stage_fixture_module",
    ROOT / "scripts" / "preview_nightly_unsigned_stage.py",
)
composition = load(
    "preview_nightly_unsigned_composition_for_tests",
    ROOT / "scripts" / "preview_nightly_unsigned_composition.py",
)

VERSION = "run-20260722-150000"
SOURCE_SHA = "a" * 40


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def unsigned_pe(*, signed: bool = False) -> bytes:
    value = bytearray(512)
    value[:2] = b"MZ"
    value[60:64] = (128).to_bytes(4, "little")
    value[128:132] = b"PE\x00\x00"
    value[148:150] = (0xE0).to_bytes(2, "little")
    optional = 152
    value[optional : optional + 2] = (0x10B).to_bytes(2, "little")
    if signed:
        value[optional + 128 : optional + 132] = (400).to_bytes(4, "little")
        value[optional + 132 : optional + 136] = (32).to_bytes(4, "little")
    return bytes(value)


def artifact(
    platform: str,
    rid: str,
    file_name: str,
    sha256: str,
    size: int,
    *,
    payload: tuple[str, str, int] | None = None,
) -> dict[str, object]:
    platform_label = "Windows X64" if platform == "windows" else "Linux X64"
    artifact_id = f"avalonia-{rid}-installer"
    download_root = (
        scope.DOWNLOAD_ROOT if platform == "windows" else "/downloads/files"
    )
    row: dict[str, object] = {
        "artifactId": artifact_id,
        "id": artifact_id,
        "fileName": file_name,
        "head": "avalonia",
        "kind": "installer",
        "platform": platform,
        "platformLabel": f"Avalonia Desktop {platform_label} Installer",
        "rid": rid,
        "arch": "x64",
        "downloadUrl": f"{download_root}/{file_name}",
        "sha256": sha256,
        "sizeBytes": size,
        "compatibilityState": "compatible",
        "compatibilityReason": None,
        "installAccessClass": "open_public",
        "installerMode": "bootstrap" if payload else "offline",
        "payloadFileName": None,
        "payloadDownloadUrl": None,
        "payloadSha256": None,
        "payloadSizeBytes": None,
    }
    if payload:
        row.update(
            {
                "payloadAcquisitionMode": "download",
                "payloadFileName": payload[0],
                "payloadDownloadUrl": f"{scope.DOWNLOAD_ROOT}/{payload[0]}",
                "payloadSha256": payload[1],
                "payloadSizeBytes": payload[2],
            }
        )
    return row


def compatibility(row: dict[str, object]) -> dict[str, object]:
    result = {
        "artifactId": row["artifactId"],
        "id": row["artifactId"],
        "fileName": row["fileName"],
        "head": row["head"],
        "kind": row["kind"],
        "flavor": row["kind"],
        "platform": row["platformLabel"],
        "platformId": row["platform"],
        "rid": row["rid"],
        "arch": row["arch"],
        "url": row["downloadUrl"],
        "sha256": row["sha256"],
        "sizeBytes": row["sizeBytes"],
        "compatibilityState": row["compatibilityState"],
        "compatibilityReason": row["compatibilityReason"],
        "installAccessClass": row["installAccessClass"],
        "installerMode": row["installerMode"],
        "payloadFileName": row["payloadFileName"],
        "payloadDownloadUrl": row["payloadDownloadUrl"],
        "payloadSha256": row["payloadSha256"],
        "payloadSizeBytes": row["payloadSizeBytes"],
    }
    if "payloadAcquisitionMode" in row:
        result["payloadAcquisitionMode"] = row["payloadAcquisitionMode"]
    return result


def fixture(tmp_path: Path) -> dict[str, object]:
    incumbent = tmp_path / "incumbent"
    incumbent_files = incumbent / "files"
    incumbent_files.mkdir(parents=True)
    incumbent_files.chmod(0o750)
    old_installer = incumbent_files / scope.INSTALLER_NAME
    old_payload = incumbent_files / scope.PAYLOAD_NAME
    old_payload_sidecar = incumbent_files / scope.PAYLOAD_SIDECAR_NAME
    linux = incumbent_files / "chummer-avalonia-linux-x64-installer.deb"
    old_installer.write_bytes(b"old-installer")
    old_payload.write_bytes(b"old-payload")
    write_json(
        old_payload_sidecar,
        scope.payload_sidecar_contract(
            "incumbent",
            digest(old_payload),
            old_payload.stat().st_size,
        ),
    )
    linux.write_bytes(b"linux-retained")
    linux.chmod(0o640)
    note = incumbent / "operator-note.txt"
    note.write_bytes(b"ancillary-retained")
    note.chmod(0o600)
    old_windows = artifact(
        "windows",
        "win-x64",
        old_installer.name,
        digest(old_installer),
        old_installer.stat().st_size,
        payload=(old_payload.name, digest(old_payload), old_payload.stat().st_size),
    )
    linux_row = artifact(
        "linux",
        "linux-x64",
        linux.name,
        digest(linux),
        linux.stat().st_size,
    )
    incumbent_identity = {
        "channel": "preview",
        "channelId": "preview",
        "contractName": "Chummer.Hub.Registry.Contracts",
        "contract_name": "Chummer.Hub.Registry.Contracts",
        "generatedAt": "2026-07-21T00:00:00Z",
        "generated_at": "2026-07-21T00:00:00Z",
        "publishedAt": "2026-07-21T00:00:00Z",
        "releaseVersion": "incumbent",
        "version": "incumbent",
    }
    write_json(
        incumbent / scope.CANONICAL_MANIFEST_NAME,
        {**incumbent_identity, "artifacts": [old_windows, linux_row]},
    )
    write_json(
        incumbent / scope.COMPATIBILITY_MANIFEST_NAME,
        {
            **incumbent_identity,
            "downloads": [compatibility(old_windows), compatibility(linux_row)],
        },
    )

    publication = tmp_path / "publication"
    shutil.copytree(incumbent, publication, copy_function=shutil.copy2)
    fresh_installer = publication / "files" / scope.INSTALLER_NAME
    fresh_payload = publication / "files" / scope.PAYLOAD_NAME
    fresh_installer.write_bytes(unsigned_pe())
    fresh_payload.write_bytes(b"fresh-payload")
    fresh_installer.chmod(0o644)
    fresh_payload.chmod(0o644)
    fresh_payload_sidecar = publication / "files" / scope.PAYLOAD_SIDECAR_NAME
    write_json(
        fresh_payload_sidecar,
        scope.payload_sidecar_contract(
            VERSION,
            digest(fresh_payload),
            fresh_payload.stat().st_size,
        ),
    )
    fresh_payload_sidecar.chmod(0o644)
    windows = artifact(
        "windows",
        "win-x64",
        fresh_installer.name,
        digest(fresh_installer),
        fresh_installer.stat().st_size,
        payload=(fresh_payload.name, digest(fresh_payload), fresh_payload.stat().st_size),
    )
    explicit = {
        "channel": "preview",
        "channelId": "preview",
        "contractName": "Chummer.Hub.Registry.Contracts",
        "contract_name": "Chummer.Hub.Registry.Contracts",
        "crossRunBitReproducible": False,
        "desktopTupleCoverage": {"requiredDesktopHeads": ["avalonia"]},
        "generatedAt": "2026-07-22T15:00:00Z",
        "generated_at": "2026-07-22T15:00:00Z",
        "platformScope": "windows_only",
        "previewPolicy": "preview_policy",
        "publicationAuthorized": False,
        "publishedAt": "2026-07-22T15:00:00Z",
        "releaseVersion": VERSION,
        "signature": dict(scope.SIGNATURE),
        "uploadAuthorized": False,
        "deployAuthorized": False,
        "version": VERSION,
    }
    windows.update(
        {
            "channel": "preview",
            "channelId": "preview",
            "crossRunBitReproducible": False,
            "platformScope": "windows_only",
            "previewPolicy": "preview_policy",
            "releaseVersion": VERSION,
            "signature": dict(scope.SIGNATURE),
            "version": VERSION,
        }
    )
    windows_download = compatibility(windows)
    windows_download.update(
        {
            "channel": "preview",
            "channelId": "preview",
            "crossRunBitReproducible": False,
            "platformScope": "windows_only",
            "previewPolicy": "preview_policy",
            "releaseVersion": VERSION,
            "signature": dict(scope.SIGNATURE),
            "version": VERSION,
        }
    )
    write_json(
        publication / scope.CANONICAL_MANIFEST_NAME,
        {**explicit, "artifacts": [linux_row, windows]},
    )
    write_json(
        publication / scope.COMPATIBILITY_MANIFEST_NAME,
        {**explicit, "downloads": [compatibility(linux_row), windows_download]},
    )

    provenance = tmp_path / "provenance"
    package_lock = provenance / "config" / "package-plane.lock.json"
    native_lock = provenance / "config" / "windows-native-bootstrap-toolchain.lock.json"
    package_lock.parent.mkdir(parents=True)
    shutil.copy2(ROOT / "config" / "package-plane.lock.json", package_lock)
    shutil.copy2(
        ROOT / "config" / "windows-native-bootstrap-toolchain.lock.json",
        native_lock,
    )
    lock_binding = {
        "sha256": digest(package_lock),
        "sizeBytes": package_lock.stat().st_size,
    }
    retained = provenance / "retained-windows-publish-closure" / "manifest.json"
    write_json(
        retained,
        {
            "atomicallyRetained": True,
            "authoritative": True,
            "consumerCommit": SOURCE_SHA,
            "contractName": "chummer6-ui.retained-windows-publish-closure",
            "contractVersion": 2,
            "deterministicRepacking": False,
            "packagePlaneLock": lock_binding,
            "publish": {
                "releaseChannel": "preview",
                "releaseVersion": VERSION,
                "status": "passed",
            },
            "release": {"channel": "preview", "version": VERSION},
            "releaseEligibility": {"eligible": False},
            "status": "passed",
        },
    )
    receipt = provenance / "UI_FRESH_PACKAGE_PLANE.generated.json"
    write_json(
        receipt,
        {
            "consumerCommit": SOURCE_SHA,
            "consumerPackagePlaneLock": lock_binding,
            "contractName": "chummer6-ui.fresh-package-plane-verification",
            "contractVersion": 11,
            "localCompatibilityTree": False,
            "mode": "integration",
            "packageCacheWasFresh": True,
            "packageSources": ["same-run-local-feed"],
            "retainedWindowsBundle": {
                "consumerCommit": SOURCE_SHA,
                "contractName": "chummer6-ui.retained-windows-publish-closure-pointer",
                "contractVersion": 2,
                "atomicallyRetained": True,
                "authority": False,
                "manifestIsAuthoritative": True,
                "manifest": {
                    "sha256": digest(retained),
                    "sizeBytes": retained.stat().st_size,
                },
                "release": {"channel": "preview", "version": VERSION},
                "status": "passed",
            },
            "status": "passed",
            "stubPackagesAllowed": False,
        },
    )
    output = tmp_path / scope.PROPOSAL_FILE_NAME
    args = argparse.Namespace(
        publication_root=publication,
        incumbent_root=incumbent,
        expected_version=VERSION,
        source_sha=SOURCE_SHA,
        package_plane_lock=package_lock,
        package_plane_receipt=receipt,
        retained_manifest=retained,
        native_toolchain_lock=native_lock,
        output=output,
    )
    return {
        "args": args,
        "fresh_installer": fresh_installer,
        "incumbent": incumbent,
        "linux": linux,
        "native_lock": native_lock,
        "output": output,
        "package_lock": package_lock,
        "publication": publication,
        "receipt": receipt,
        "retained": retained,
    }


def test_builds_exact_non_authoritative_v3_scope(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    proposal = scope.build_proposal(values["args"])
    assert set(proposal) == scope.ROOT_KEYS
    assert proposal["contractName"] == (
        "chummer6-ui.preview-nightly-unsigned-publication-scope"
    )
    assert proposal["contractVersion"] == 3
    assert proposal["status"] == "prepared"
    assert proposal["release"] == {"channel": "preview", "version": VERSION}
    assert proposal["platformScope"] == "windows_only"
    assert proposal["crossRunBitReproducible"] is False
    assert proposal["signature"] == scope.SIGNATURE
    assert proposal["publicationAuthorized"] is False
    assert proposal["uploadAuthorized"] is False
    assert proposal["deployAuthorized"] is False
    assert proposal["projectionProfile"] == scope.PROJECTION_PROFILE
    assert [row["artifactRole"] for row in proposal["freshDelta"]] == [
        "installer",
        "bootstrap_payload",
        "bootstrap_payload_sidecar",
    ]
    assert all(set(row) == {
        "artifactRole", "fileName", "head", "mode", "path", "platform",
        "rid", "sha256", "sizeBytes"
    } for row in proposal["freshDelta"])
    assert {row["retentionKind"] for row in proposal["retainedFromIncumbent"]} == {
        "ancillary",
        "managed_artifact",
    }
    assert all(set(value) == {"sha256", "sizeBytes"} for value in proposal["provenance"].values())
    forbidden = {
        "authenticodeRequired",
        "signingReceipt",
        "nativeEvidence",
        "visualApproval",
        "humanApproval",
        "macosSoak",
        "stableRelease",
    }
    assert forbidden.isdisjoint(proposal)


def test_prepare_and_verify_replay_exact_scope(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    proposal = scope.build_proposal(values["args"])
    scope.write_scope(values["output"], proposal)
    assert stat.S_IMODE(values["output"].stat().st_mode) == 0o600
    observed = scope.read_json(values["output"], "scope")
    assert scope.validate_proposal(observed) == proposal
    replay = scope.build_proposal(values["args"])
    assert observed == replay


def test_non_windows_managed_byte_change_fails_closed(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    target = values["publication"] / "files" / values["linux"].name
    target.write_bytes(b"changed-linux")
    with pytest.raises(scope.ScopeError, match="artifact bytes|non-Windows managed"):
        scope.build_proposal(values["args"])


def test_directory_mode_change_fails_closed(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    (values["publication"] / "files").chmod(0o700)
    with pytest.raises(scope.ScopeError, match="directory set or modes"):
        scope.build_proposal(values["args"])


def test_signed_pe_fails_closed(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    values["fresh_installer"].write_bytes(unsigned_pe(signed=True))
    with pytest.raises(scope.ScopeError, match="not unsigned"):
        scope.build_proposal(values["args"])


def test_payload_sidecar_must_bind_fresh_version_and_payload(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    sidecar_path = (
        values["publication"] / "files" / scope.PAYLOAD_SIDECAR_NAME
    )
    sidecar = json.loads(sidecar_path.read_text())
    sidecar["releaseVersion"] = "stale-incumbent"
    write_json(sidecar_path, sidecar)
    with pytest.raises(scope.ScopeError, match="metadata sidecar differs"):
        scope.build_proposal(values["args"])


def test_payload_sidecar_is_mandatory_fresh_delta_custody(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    (
        values["publication"] / "files" / scope.PAYLOAD_SIDECAR_NAME
    ).unlink()
    with pytest.raises(scope.ScopeError, match="payload metadata sidecar"):
        scope.build_proposal(values["args"])


def test_compatibility_payload_url_must_bind_exact_fresh_payload(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    path = values["publication"] / scope.COMPATIBILITY_MANIFEST_NAME
    manifest = json.loads(path.read_text())
    windows = next(
        row
        for row in manifest["downloads"]
        if row["fileName"] == scope.INSTALLER_NAME
    )
    windows["payloadDownloadUrl"] = "https://attacker.invalid/payload.zip"
    write_json(path, manifest)
    with pytest.raises(scope.ScopeError, match="Windows compatibility projection differs"):
        scope.build_proposal(values["args"])


def test_compatibility_installer_url_aliases_cannot_conflict(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    path = values["publication"] / scope.COMPATIBILITY_MANIFEST_NAME
    manifest = json.loads(path.read_text())
    windows = next(
        row
        for row in manifest["downloads"]
        if row["fileName"] == scope.INSTALLER_NAME
    )
    windows["downloadUrl"] = "https://attacker.invalid/installer.exe"
    write_json(path, manifest)
    with pytest.raises(scope.ScopeError, match="Windows compatibility projection differs"):
        scope.build_proposal(values["args"])


@pytest.mark.parametrize(
    "manifest_name,field",
    [
        (scope.CANONICAL_MANIFEST_NAME, "publicationAuthorized"),
        (scope.CANONICAL_MANIFEST_NAME, "uploadAuthorized"),
        (scope.CANONICAL_MANIFEST_NAME, "deployAuthorized"),
        (scope.COMPATIBILITY_MANIFEST_NAME, "publicationAuthorized"),
        (scope.COMPATIBILITY_MANIFEST_NAME, "uploadAuthorized"),
        (scope.COMPATIBILITY_MANIFEST_NAME, "deployAuthorized"),
    ],
)
def test_manifest_authority_posture_must_be_explicit_false(
    tmp_path: Path, manifest_name: str, field: str
) -> None:
    values = fixture(tmp_path)
    path = values["publication"] / manifest_name
    manifest = json.loads(path.read_text())
    del manifest[field]
    write_json(path, manifest)
    with pytest.raises(scope.ScopeError, match=f"{field} must be explicit false"):
        scope.build_proposal(values["args"])


def test_package_receipt_must_bind_source_and_retained_manifest(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    receipt = json.loads(values["receipt"].read_text())
    receipt["consumerCommit"] = "b" * 40
    write_json(values["receipt"], receipt)
    with pytest.raises(scope.ScopeError, match="receipt authority"):
        scope.build_proposal(values["args"])


@pytest.mark.parametrize(("document", "version"), (("lock", 10), ("receipt", 10)))
def test_package_plane_provenance_requires_exact_v11_contract(
    tmp_path: Path, document: str, version: int
) -> None:
    values = fixture(tmp_path)
    path = values["package_lock" if document == "lock" else "receipt"]
    payload = json.loads(path.read_text())
    payload["contractVersion"] = version
    write_json(path, payload)

    with pytest.raises(scope.ScopeError, match="package-plane .* differs"):
        scope.build_proposal(values["args"])


def test_native_lock_tamper_fails_semantic_validation(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    native = json.loads(values["native_lock"].read_text())
    native["platform"]["architecture"] = "arm64"
    write_json(values["native_lock"], native)
    with pytest.raises(scope.ScopeError, match="native toolchain"):
        scope.build_proposal(values["args"])


def test_full_inventory_and_fresh_delta_are_cryptographically_coupled(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    proposal = scope.build_proposal(values["args"])
    proposal["freshDelta"][0]["sha256"] = "f" * 64
    with pytest.raises(scope.ScopeError, match="freshDelta differs"):
        scope.validate_proposal(proposal)


def test_projection_profile_is_exact_and_non_authoritative(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    proposal = scope.build_proposal(values["args"])
    proposal["projectionProfile"] = "legacy_byte_copy"
    with pytest.raises(scope.ScopeError, match="posture differs"):
        scope.validate_proposal(proposal)

    request = composition.build_request(values["args"])
    request["projectionProfile"] = "legacy_byte_copy"
    with pytest.raises(composition.CompositionError, match="posture differs"):
        composition.validate_request(request)


def test_projected_manifest_pair_must_agree_on_profile(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    canonical_path = values["publication"] / scope.CANONICAL_MANIFEST_NAME
    canonical = json.loads(canonical_path.read_text())
    canonical["projectionProfile"] = scope.PROJECTION_PROFILE
    write_json(canonical_path, canonical)
    with pytest.raises(scope.ScopeError, match="projection profiles disagree"):
        scope.build_proposal(values["args"])

    canonical["projectionProfile"] = "unsupported_projection"
    write_json(canonical_path, canonical)
    with pytest.raises(scope.ScopeError, match="projection profile is unsupported"):
        scope.build_proposal(values["args"])


def test_scope_output_is_exclusive(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    proposal = scope.build_proposal(values["args"])
    values["output"].write_text("do-not-overwrite", encoding="utf-8")
    with pytest.raises(scope.ScopeError, match="must be absent"):
        scope.write_scope(values["output"], proposal)
    assert values["output"].read_text() == "do-not-overwrite"


def test_builds_exact_windows_only_composition_v3(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    request = composition.build_request(values["args"])
    assert set(request) == composition.ROOT_KEYS
    assert request["contractName"] == (
        "chummer6-ui.preview-nightly-unsigned-composition-request"
    )
    assert request["contractVersion"] == 3
    assert request["release"] == {"channel": "preview", "version": VERSION}
    assert request["platformScope"] == "windows_only"
    assert request["signature"] == scope.SIGNATURE
    assert request["publicationAuthorized"] is False
    assert request["uploadAuthorized"] is False
    assert request["deployAuthorized"] is False
    assert request["projectionProfile"] == scope.PROJECTION_PROFILE
    assert request["incumbentSnapshot"]["fullShelfInventorySha256"] == (
        request["incumbentSnapshot"]["fullShelfInventorySha256"]
    )
    assert request["proposedDirectoryModes"] == scope.directory_modes(
        values["publication"]
    )
    assert [row["artifactRole"] for row in request["freshDelta"]] == [
        "installer",
        "bootstrap_payload",
        "bootstrap_payload_sidecar",
    ]
    assert len({row["manifestRowSha256"] for row in request["freshDelta"]}) == 1
    assert all(
        set(binding) == {"path", "sha256", "sizeBytes"}
        for binding in request["provenance"].values()
    )
    assert request["provenance"]["packagePlaneLock"]["path"] == (
        "provenance/config/package-plane.lock.json"
    )


def test_composition_replay_and_snapshot_tamper_fail_closed(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    request = composition.build_request(values["args"])
    output = tmp_path / composition.PROPOSAL_FILE_NAME
    composition.write_request(output, request)
    observed = scope.read_json(output, "composition")
    assert composition.validate_request(observed) == request
    assert observed == composition.build_request(values["args"])
    observed["incumbentSnapshot"]["directoryModes"][0]["mode"] ^= 1
    with pytest.raises(composition.CompositionError, match="directory-mode digest"):
        composition.validate_request(observed)


def test_composition_manifest_row_digest_is_bound(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    request = composition.build_request(values["args"])
    request["freshDelta"][1]["manifestRowSha256"] = "f" * 64
    with pytest.raises(composition.CompositionError, match="share one"):
        composition.validate_request(request)
