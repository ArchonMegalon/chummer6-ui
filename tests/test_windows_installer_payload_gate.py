from __future__ import annotations

import hashlib
import json
import os
import shutil
import stat
import struct
import subprocess
import warnings
import zipfile
from datetime import datetime, timezone
from pathlib import Path

import pytest


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
VERIFY_SCRIPT = REPO_ROOT / "scripts" / "verify-windows-installer-payloads.py"
PUBLISH_SCRIPT = REPO_ROOT / "scripts" / "publish-download-bundle.sh"
APPENDED_PAYLOAD_MAGIC = b"CHUMMER6PAYLOAD1"
BOOTSTRAP_METADATA_MARKER = b"\nCHUMMER6_BOOTSTRAP_METADATA\n"
BOOTSTRAP_ZIP_POLICY_VERSION = "chummer6.windows-bootstrap-zip-admission.v1"
MAX_PAYLOAD_ZIP_INSPECTABLE_CONTENT_BYTES = 16 * 1024 * 1024
MUTATING_PUBLISH_ENV_PATHS = (
    "PORTAL_MANIFEST_PATH",
    "PORTAL_CANONICAL_MANIFEST_PATH",
    "PORTAL_DOWNLOADS_DIR",
    "PRESENTATION_MIRROR_ROOT",
    "RUN_SERVICES_DOWNLOADS_ROOT",
    "REGISTRY_CANONICAL_MANIFEST_PATH",
    "REGISTRY_RELEASES_MANIFEST_PATH",
    "REGISTRY_FILES_DIR",
    "QUARANTINE_PROMOTION_EVIDENCE_PATH",
    "CHUMMER_UI_EXTERNAL_HOST_PROOF_BLOCKERS_PATH",
    "CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH",
    "CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH",
    "CHUMMER_UI_WORKFLOW_PARITY_PATH",
    "CHUMMER_SR4_WORKFLOW_PARITY_PATH",
    "CHUMMER_SR6_WORKFLOW_PARITY_PATH",
    "CHUMMER_PUBLIC_EDGE_DOWNLOADS_MIRROR_DIRS",
)


def _assert_publish_env_isolated(tmp_path: Path, env: dict[str, str]) -> None:
    isolated_root = tmp_path.resolve()
    for key in MUTATING_PUBLISH_ENV_PATHS:
        configured_values = [
            value.strip() for value in env[key].split(",") if value.strip()
        ]
        assert configured_values, f"{key} must have an isolated test path"
        for value in configured_values:
            resolved = Path(value).resolve(strict=False)
            assert resolved == isolated_root or isolated_root in resolved.parents, (
                f"{key} escapes pytest tmp_path: {resolved}"
            )


def _publish_env(tmp_path: Path, **overrides: str) -> dict[str, str]:
    side_effect_root = tmp_path / "publish-side-effects"
    portal_root = side_effect_root / "portal"
    registry_root = side_effect_root / "registry"
    workbench_proof_path = (
        side_effect_root / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"
    )
    browser_lane_proof_path = (
        side_effect_root / "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json"
    )
    source_workbench_proof = (
        REPO_ROOT
        / ".codex-studio"
        / "published"
        / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"
    )
    if source_workbench_proof.is_file():
        workbench_proof_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_workbench_proof, workbench_proof_path)
    browser_lane_proof_path.parent.mkdir(parents=True, exist_ok=True)
    browser_lane_proof_path.write_text(
        json.dumps(
            {
                "contract_name": "chummer6-ui.blazor_browser_lane_proof_set",
                "status": "pass",
            }
        )
        + "\n",
        encoding="utf-8",
    )
    workflow_proof_specs = {
        "CHUMMER_UI_WORKFLOW_PARITY_PATH": (
            "CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json",
            "chummer6-ui.chummer5a_desktop_workflow_parity",
        ),
        "CHUMMER_SR4_WORKFLOW_PARITY_PATH": (
            "SR4_DESKTOP_WORKFLOW_PARITY.generated.json",
            "chummer6-ui.sr4_desktop_workflow_parity",
        ),
        "CHUMMER_SR6_WORKFLOW_PARITY_PATH": (
            "SR6_DESKTOP_WORKFLOW_PARITY.generated.json",
            "chummer6-ui.sr6_desktop_workflow_parity",
        ),
    }
    workflow_proof_paths: dict[str, Path] = {}
    for environment_key, (file_name, contract_name) in workflow_proof_specs.items():
        proof_path = side_effect_root / file_name
        proof_path.write_text(
            json.dumps({"contract_name": contract_name, "status": "pass"}) + "\n",
            encoding="utf-8",
        )
        workflow_proof_paths[environment_key] = proof_path
    env = {
        "PATH": "/usr/bin:/bin",
        "PORTAL_MANIFEST_PATH": str(portal_root / "releases.json"),
        "PORTAL_CANONICAL_MANIFEST_PATH": str(
            portal_root / "RELEASE_CHANNEL.generated.json"
        ),
        "PORTAL_DOWNLOADS_DIR": str(portal_root),
        "PRESENTATION_MIRROR_ROOT": str(side_effect_root / "presentation"),
        "RUN_SERVICES_DOWNLOADS_ROOT": str(side_effect_root / "run-services"),
        "CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH": str(
            side_effect_root / "run-services-release-channel.input.json"
        ),
        "REGISTRY_CANONICAL_MANIFEST_PATH": str(
            registry_root / "RELEASE_CHANNEL.generated.json"
        ),
        "REGISTRY_RELEASES_MANIFEST_PATH": str(registry_root / "releases.json"),
        "REGISTRY_FILES_DIR": str(registry_root / "files"),
        "QUARANTINE_PROMOTION_EVIDENCE_PATH": str(
            side_effect_root / "QUARANTINED_INSTALLER_PROMOTION.generated.json"
        ),
        "CHUMMER_UI_EXTERNAL_HOST_PROOF_BLOCKERS_PATH": str(
            side_effect_root / "UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json"
        ),
        "CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH": str(workbench_proof_path),
        "CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH": str(browser_lane_proof_path),
        **{
            environment_key: str(proof_path)
            for environment_key, proof_path in workflow_proof_paths.items()
        },
        "CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS": "false",
        "CHUMMER_PUBLIC_EDGE_DOWNLOADS_MIRROR_DIRS": str(
            side_effect_root / "publisher-mirrors"
        ),
        **overrides,
    }
    _assert_publish_env_isolated(tmp_path, env)
    return env


def test_publish_env_routes_every_mutating_output_under_tmp_path(
    tmp_path: Path,
) -> None:
    env = _publish_env(tmp_path)

    _assert_publish_env_isolated(tmp_path, env)
    assert env["CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS"] == "false"
    assert str(REPO_ROOT.parent / "chummer.run-services") not in "\n".join(
        env[key] for key in MUTATING_PUBLISH_ENV_PATHS
    )
    assert str(REPO_ROOT.parent / "chummer-hub-registry") not in "\n".join(
        env[key] for key in MUTATING_PUBLISH_ENV_PATHS
    )


def _fresh_root_blocker_generated_at() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def _write_bootstrap_payload(payload_path: Path, *, launch_executable: str = "Chummer.Avalonia.exe") -> bytes:
    with zipfile.ZipFile(payload_path, "w") as archive:
        archive.writestr(launch_executable, b"placeholder")
        archive.writestr("Samples/Legacy/Soma-Career.chum5", b"sample")
    return payload_path.read_bytes()


def _run_appended_payload_verifier(
    tmp_path: Path,
    entries: list[tuple[str | zipfile.ZipInfo, bytes]],
    *,
    compression: int = zipfile.ZIP_STORED,
    environment: dict[str, str] | None = None,
    raw_payload: bytes | None = None,
) -> subprocess.CompletedProcess[str]:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    payload_path = tmp_path / "payload.zip"
    if raw_payload is None:
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            with zipfile.ZipFile(payload_path, "w", compression=compression) as archive:
                for entry_name, contents in entries:
                    archive.writestr(entry_name, contents, compress_type=compression)
        payload_bytes = payload_path.read_bytes()
    else:
        payload_bytes = raw_payload

    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(
        (b"installer-stub" * 200)
        + payload_bytes
        + struct.pack("<q", len(payload_bytes))
        + APPENDED_PAYLOAD_MAGIC
    )
    subprocess_environment = None
    if environment is not None:
        subprocess_environment = {**os.environ, **environment}
    return subprocess.run(
        ["python3", str(VERIFY_SCRIPT), "--files-dir", str(files_dir)],
        text=True,
        capture_output=True,
        check=False,
        env=subprocess_environment,
    )


def _assert_redacted_zip_entry_failure(
    result: subprocess.CompletedProcess[str],
    raw_name: str,
    expected_rule: str,
    *,
    ordinal: int,
) -> None:
    name_digest = hashlib.sha256(
        raw_name.encode("utf-8", errors="surrogatepass")
    ).hexdigest()
    assert result.returncode != 0
    assert expected_rule in result.stderr
    assert BOOTSTRAP_ZIP_POLICY_VERSION in result.stderr
    assert f"entry_ordinal={ordinal} entry_name_sha256={name_digest}" in result.stderr
    assert raw_name not in result.stderr
    assert json.dumps(raw_name, ensure_ascii=True) not in result.stderr


def _mark_first_zip_entry_encrypted(payload: bytes) -> bytes:
    modified = bytearray(payload)
    local_header = modified.find(b"PK\x03\x04")
    central_header = modified.find(b"PK\x01\x02")
    assert local_header >= 0 and central_header >= 0
    local_flags = struct.unpack_from("<H", modified, local_header + 6)[0]
    central_flags = struct.unpack_from("<H", modified, central_header + 8)[0]
    struct.pack_into("<H", modified, local_header + 6, local_flags | 0x1)
    struct.pack_into("<H", modified, central_header + 8, central_flags | 0x1)
    return bytes(modified)


def _mutate_first_zip_local_header(
    payload: bytes,
    *,
    add_flags: int = 0,
    compression_method: int | None = None,
    replacement_name: str | None = None,
    filename_length: int | None = None,
) -> bytes:
    modified = bytearray(payload)
    local_header = modified.find(b"PK\x03\x04")
    assert local_header >= 0
    if add_flags:
        local_flags = struct.unpack_from("<H", modified, local_header + 6)[0]
        struct.pack_into("<H", modified, local_header + 6, local_flags | add_flags)
    if compression_method is not None:
        struct.pack_into("<H", modified, local_header + 8, compression_method)

    observed_name_length = struct.unpack_from("<H", modified, local_header + 26)[0]
    if replacement_name is not None:
        replacement_bytes = replacement_name.encode("utf-8")
        assert len(replacement_bytes) == observed_name_length
        name_start = local_header + 30
        modified[name_start : name_start + observed_name_length] = replacement_bytes
    if filename_length is not None:
        struct.pack_into("<H", modified, local_header + 26, filename_length)
    return bytes(modified)


def _mutate_eocd_central_directory_size(payload: bytes, size: int) -> bytes:
    modified = bytearray(payload)
    eocd_offset = modified.rfind(b"PK\x05\x06")
    assert eocd_offset >= 0
    struct.pack_into("<L", modified, eocd_offset + 12, size)
    return bytes(modified)


def _corrupt_first_zip_entry_data(payload: bytes) -> bytes:
    modified = bytearray(payload)
    local_header = modified.find(b"PK\x03\x04")
    assert local_header >= 0
    file_name_length = struct.unpack_from("<H", modified, local_header + 26)[0]
    extra_length = struct.unpack_from("<H", modified, local_header + 28)[0]
    data_offset = local_header + 30 + file_name_length + extra_length
    assert data_offset < len(modified)
    modified[data_offset] ^= 0x01
    return bytes(modified)


def _write_bootstrap_installer(
    installer_path: Path,
    *,
    payload_download_url: str,
    payload_sha256: str,
    payload_size_bytes: int,
    payload_acquisition_mode: str = "",
) -> None:
    installer_path.write_bytes(
        b"installer-stub\n"
        + (b"installer-padding" * 200)
        + BOOTSTRAP_METADATA_MARKER
        + f"payloadDownloadUrl={payload_download_url}\n".encode("utf-8")
        + f"payloadSha256={payload_sha256}\n".encode("utf-8")
        + f"payloadSizeBytes={payload_size_bytes}\n".encode("utf-8")
        + (
            f"payloadAcquisitionMode={payload_acquisition_mode}\n".encode("utf-8")
            if payload_acquisition_mode
            else b""
        )
    )


def _write_bundle_manifest(
    manifest_path: Path,
    *,
    installer_name: str,
    installer_sha256: str = "installer-sha-placeholder",
    installer_size_bytes: int = 1,
    payload_name: str = "",
    payload_sha256: str = "",
    payload_size_bytes: int = 0,
    installer_mode: str = "bootstrap",
    payload_download_url: str | None = None,
    payload_acquisition_mode: str = "",
) -> None:
    payload = {
        "version": "run-test",
        "channel": "preview",
        "publishedAt": "2026-06-24T00:00:00Z",
        "downloads": [
            {
                "artifactId": "avalonia-win-x64-installer",
                "fileName": installer_name,
                "url": f"https://example.invalid/downloads/files/{installer_name}",
                "sha256": installer_sha256,
                "sizeBytes": installer_size_bytes,
                "kind": "installer",
                "platform": "windows",
                "installerMode": installer_mode,
                "payloadFileName": payload_name,
                "payloadDownloadUrl": payload_download_url or (f"https://example.invalid/downloads/files/{payload_name}" if payload_name else ""),
                "payloadSha256": payload_sha256,
                "payloadSizeBytes": payload_size_bytes,
            }
        ],
    }
    if payload_acquisition_mode:
        payload["downloads"][0]["payloadAcquisitionMode"] = payload_acquisition_mode
    manifest_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def _canonical_json_sha256(payload: object) -> str:
    encoded = json.dumps(payload, separators=(",", ":"), sort_keys=True).encode(
        "utf-8"
    )
    return hashlib.sha256(encoded).hexdigest()


def _write_test_mac_build_provenance(
    bundle_dir: Path,
    *,
    artifact_path: Path,
    artifact_id: str = "avalonia-osx-arm64-installer",
    head: str = "avalonia",
) -> None:
    """Materialize a complete, internally bound Mac provenance fixture."""

    target_id = {
        "avalonia": "desktop-avalonia",
        "blazor-desktop": "desktop-blazor",
    }[head]
    artifact_sha256 = hashlib.sha256(artifact_path.read_bytes()).hexdigest()
    artifact_size_bytes = artifact_path.stat().st_size
    generated_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    invocation_id = f"pytest-{artifact_id}"
    source_commit = "1" * 40
    source_tree = "2" * 40
    tool_sha256 = "3" * 64

    source_manifest = {
        "status": "published",
        "version": "run-test",
        "channel": "preview",
        "channelId": "preview",
        "artifacts": [
            {
                "artifactId": artifact_id,
                "fileName": artifact_path.name,
                "platform": "macos",
                "head": head,
                "rid": "osx-arm64",
                "arch": "arm64",
                "sha256": artifact_sha256,
                "sizeBytes": artifact_size_bytes,
                "kind": "installer",
            }
        ],
    }
    (bundle_dir / "RELEASE_CHANNEL.generated.json").write_text(
        json.dumps(source_manifest, indent=2) + "\n",
        encoding="utf-8",
    )

    provenance_root = bundle_dir / "proof" / "build-provenance" / "v1"
    invocation_root = provenance_root / "invocations"
    sbom_root = provenance_root / "sbom"
    invocation_root.mkdir(parents=True)
    sbom_root.mkdir()

    sbom = {
        "bomFormat": "CycloneDX",
        "specVersion": "1.5",
        "serialNumber": f"urn:uuid:pytest-{target_id}",
        "version": 1,
        "metadata": {
            "component": {
                "type": "application",
                "bom-ref": f"urn:chummer:project:{target_id}",
                "name": target_id,
                "version": "0.0.0-test",
            }
        },
        "components": [],
        "dependencies": [],
    }
    sbom_path = sbom_root / f"{target_id}.cdx.json"
    sbom_path.write_text(json.dumps(sbom, indent=2) + "\n", encoding="utf-8")
    sbom_sha256 = hashlib.sha256(sbom_path.read_bytes()).hexdigest()

    state = {
        "builder_id": "chummer-mac-hosted-bootstrap",
        "build_type": "macos-desktop-release",
        "invocation_id": invocation_id,
        "started_at_utc": generated_at,
        "started_epoch_ns": 1,
        "build_tools": {
            "provenance_generator_sha256": tool_sha256,
            "supply_chain_verifier_sha256": tool_sha256,
        },
        "build_inputs": [
            {"label": label, "sha256": tool_sha256}
            for label in (
                "hosted-bootstrap",
                "desktop-project",
                "desktop-installer-recipe",
                "dotnet-sdk-selection",
            )
        ],
        "source": {
            "repository": "chummer-presentation",
            "commit": source_commit,
            "tree": source_tree,
            "tracked_worktree_dirty": False,
        },
        "source_materials": [
            {
                "repository": repository,
                "commit": source_commit,
                "tree": source_tree,
                "tracked_worktree_dirty": False,
            }
            for repository in (
                "chummer-core-engine",
                "chummer.run-services",
                "chummer-ui-kit",
                "chummer-hub-registry",
                "chummer-media-factory",
                "chummer5a",
            )
        ],
        "subject_declaration": {
            "artifact_id": artifact_id,
            "artifact_name": artifact_path.name,
            "artifact_kind": "desktop_download",
            "artifact_binding_type": "file",
            "artifact_path": str(artifact_path),
            "target_id": target_id,
            "prebuild": {"exists": False},
        },
        "sbom": {
            "sha256": sbom_sha256,
            "generator": "deterministic_project.assets.json_inventory.v1",
        },
    }
    receipt = {
        "contract_name": "chummer6.build_provenance.v1",
        "receipt_kind": "invocation",
        "status": "pass",
        "builder_id": "chummer-mac-hosted-bootstrap",
        "build_type": "macos-desktop-release",
        "invocation_id": invocation_id,
        "generated_at_utc": generated_at,
        "build_started_at_utc": generated_at,
        "failures": [],
        "invocation": {
            "state_contract_name": "chummer6.build_provenance_invocation_state.v1",
            "subject_declared_before_build": True,
            "source_identity_stable": True,
            "state_sha256": _canonical_json_sha256(state),
            "state": state,
        },
        "subjects": [
            {
                "artifact_id": artifact_id,
                "artifact_kind": "desktop_download",
                "artifact_name": artifact_path.name,
                "artifact_sha256": artifact_sha256,
                "artifact_size_bytes": artifact_size_bytes,
                "target_id": target_id,
                "source_repository": "chummer-presentation",
                "source_commit": source_commit,
                "source_tree": source_tree,
                "invocation_id": invocation_id,
                "produced_during_invocation": True,
                "source_tracked_worktree_dirty": False,
                "artifact_built_mtime_ns": 2,
                "sbom_sha256": sbom_sha256,
                "sbom_generator": "deterministic_project.assets.json_inventory.v1",
            }
        ],
    }
    (invocation_root / f"{invocation_id}.json").write_text(
        json.dumps(receipt, indent=2) + "\n",
        encoding="utf-8",
    )


def _append_plain_desktop_installer(
    bundle_dir: Path,
    *,
    platform: str,
) -> tuple[Path, str]:
    platform_fixture = {
        "linux": (
            "avalonia-linux-x64-installer",
            "chummer-avalonia-linux-x64-installer.deb",
            "linux-x64",
            b"linux-installer-placeholder",
        ),
        "macos": (
            "avalonia-osx-arm64-installer",
            "chummer-avalonia-osx-arm64-installer.dmg",
            "osx-arm64",
            b"macos-installer-placeholder",
        ),
    }[platform]
    artifact_id, file_name, rid, contents = platform_fixture
    artifact_path = bundle_dir / "files" / file_name
    artifact_path.write_bytes(contents)
    artifact_sha256 = hashlib.sha256(contents).hexdigest()

    manifest_path = bundle_dir / "releases.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["downloads"].append(
        {
            "artifactId": artifact_id,
            "fileName": file_name,
            "url": f"https://example.invalid/downloads/files/{file_name}",
            "sha256": artifact_sha256,
            "sizeBytes": len(contents),
            "kind": "installer",
            "platform": platform,
            "head": "avalonia",
            "rid": rid,
        }
    )
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    if platform == "macos":
        _write_test_mac_build_provenance(
            bundle_dir,
            artifact_path=artifact_path,
        )
    return artifact_path, artifact_sha256


def _write_plain_installer_startup_smoke(
    bundle_dir: Path,
    *,
    artifact_path: Path,
    artifact_sha256: str,
    platform: str,
    recorded_at: str,
) -> None:
    platform_fixture = {
        "linux": ("x64", "linux-x64", "linux-x64-container", "Linux 6.0.0"),
        "macos": ("arm64", "osx-arm64", "macos-arm64-host", "macOS 15.0"),
    }[platform]
    arch, rid, host_class, operating_system = platform_fixture
    startup_smoke_dir = bundle_dir / "startup-smoke"
    startup_smoke_dir.mkdir(exist_ok=True)
    (startup_smoke_dir / f"startup-smoke-avalonia-{rid}.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": platform,
                "arch": arch,
                "rid": rid,
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": host_class,
                "operatingSystem": operating_system,
                "artifactDigest": f"sha256:{artifact_sha256}",
                "artifactSha256": artifact_sha256,
                "artifactFileName": artifact_path.name,
                "fileName": artifact_path.name,
                "artifactRelativePath": f"files/{artifact_path.name}",
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )


def _write_release_proof_fixture(path: Path) -> None:
    path.write_text(
        json.dumps(
            {
                "contractName": "chummer6-hub.local_release_proof",
                "status": "passed",
                "generatedAt": datetime.now(timezone.utc)
                .isoformat()
                .replace("+00:00", "Z"),
                "baseUrl": "https://example.invalid",
                "journeysPassed": [
                    "install_claim_restore_continue",
                    "build_explain_publish",
                    "campaign_session_recover_recap",
                    "report_cluster_release_notify",
                    "organize_community_and_close_loop",
                ],
                "proofRoutes": [
                    "/downloads/install/avalonia-linux-x64-installer",
                    "/home/access",
                    "/home/work",
                    "/account/access",
                    "/account/work",
                    "/account/roster",
                    "/account/support",
                    "/contact",
                    "/downloads",
                    "/downloads/install/avalonia-osx-arm64-installer",
                    "/downloads/install/avalonia-win-x64-installer",
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )


def test_windows_installer_verifier_accepts_bootstrap_payload(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    payload_sidecar = files_dir / "chummer-avalonia-win-x64-payload.zip.json"
    payload_sidecar.write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert "windows_installer_payload_gate:ok checked=1" in result.stdout


def test_windows_installer_verifier_rejects_bootstrap_installer_with_malformed_embedded_payload_url(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=f"\\{payload_path.name}",
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
        payload_download_url=payload_url,
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap installer embedded payloadDownloadUrl must be an absolute file, http, or https URL" in result.stderr
    assert "bootstrap installer embedded payloadDownloadUrl does not match manifest/sidecar metadata" in result.stderr


def test_windows_installer_verifier_rejects_oversized_bootstrap_installer(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    with installer_path.open("ab") as handle:
        handle.truncate((15 * 1024 * 1024) + 1)
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap installer is too large" in result.stderr


def test_windows_installer_verifier_accepts_oversized_self_contained_embedded_bootstrap(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
        payload_acquisition_mode="embedded",
    )
    with installer_path.open("ab") as handle:
        handle.truncate((15 * 1024 * 1024) + 1)
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "payloadAcquisitionMode": "embedded",
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
        payload_acquisition_mode="embedded",
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert b"payloadAcquisitionMode=embedded" in installer_path.read_bytes()


def test_windows_installer_verifier_rejects_installer_without_manifest_row_when_required(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name="chummer-blazor-desktop-win-x64-installer.exe",
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
            "--require-manifest-row",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Windows installer is missing from the supplied release manifest" in result.stderr


def test_windows_installer_verifier_rejects_bootstrap_payload_without_sidecar_metadata(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=f"https://example.invalid/downloads/files/{payload_path.name}",
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap payload sidecar metadata is missing" in result.stderr


def test_windows_installer_verifier_rejects_mismatched_bootstrap_sidecar_metadata(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=f"https://example.invalid/downloads/files/{payload_path.name}",
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": f"https://example.invalid/downloads/files/{payload_path.name}",
                "sha256": "wrong",
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap payload sidecar metadata sha256 does not match payload bytes" in result.stderr


def test_windows_installer_verifier_rejects_bootstrap_installer_without_embedded_payload_metadata(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap installer does not contain embedded payloadDownloadUrl metadata" in result.stderr


def test_windows_installer_verifier_uses_sidecar_as_embedded_metadata_truth_before_manifest_exists(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap installer does not contain embedded payloadDownloadUrl metadata" in result.stderr


def test_windows_installer_verifier_rejects_bootstrap_sidecar_with_non_https_download_url_without_manifest(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"http://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        ["python3", str(VERIFY_SCRIPT), "--files-dir", str(files_dir)],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "bootstrap payload sidecar metadata downloadUrl must be an absolute HTTPS URL" in result.stderr


def test_windows_installer_verifier_rejects_bootstrap_manifest_with_bad_payload_url(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"http://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
        payload_download_url=payload_url,
    )

    result = subprocess.run(
        ["python3", str(VERIFY_SCRIPT), "--files-dir", str(files_dir), "--manifest", str(manifest_path)],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "manifest payloadDownloadUrl must be an absolute HTTPS URL" in result.stderr


def test_windows_installer_verifier_accepts_origin_root_relative_payload_url(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"/downloads/g/test/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
        payload_download_url=payload_url,
    )

    result = subprocess.run(
        ["python3", str(VERIFY_SCRIPT), "--files-dir", str(files_dir), "--manifest", str(manifest_path)],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr


def test_windows_installer_verifier_accepts_generation_and_stable_payload_url_aliases(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    stable_payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    generation_payload_url = f"/downloads/g/test/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=stable_payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": stable_payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    manifest_path = tmp_path / "releases.json"
    _write_bundle_manifest(
        manifest_path,
        installer_name=installer_path.name,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
        payload_download_url=generation_payload_url,
    )

    result = subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--files-dir",
            str(files_dir),
            "--manifest",
            str(manifest_path),
            "--require-embedded-bootstrap-metadata",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr


def test_windows_installer_verifier_accepts_appended_payload(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_zip_path = tmp_path / "payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_zip_path)
    installer_path.write_bytes(
        (b"installer-stub" * 200)
        + payload_bytes
        + struct.pack("<q", len(payload_bytes))
        + APPENDED_PAYLOAD_MAGIC
    )

    result = subprocess.run(
        ["python3", str(VERIFY_SCRIPT), "--files-dir", str(files_dir)],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert "windows_installer_payload_gate:ok checked=1" in result.stdout


def test_windows_installer_verifier_accepts_clean_public_key_and_noncredential_configuration(
    tmp_path: Path,
) -> None:
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            (
                "config/settings.json",
                json.dumps(
                    {
                        "theme": "dark",
                        "api_base_url": "https://example.invalid/api",
                    }
                ).encode("utf-8"),
            ),
            (
                "docs/public-key.pem",
                b"-----BEGIN PUBLIC KEY-----\nnot-private\n-----END PUBLIC KEY-----\n",
            ),
        ],
    )

    assert result.returncode == 0, result.stderr
    assert "windows_installer_payload_gate:ok checked=1" in result.stdout


@pytest.mark.parametrize(
    ("configuration", "expected_rule", "sentinel"),
    [
        ({"client_secret": "REDACTED"}, "content.credential_assignment", "REDACTED"),
        ({"access_token": "placeholder"}, "content.credential_assignment", "placeholder"),
        ({"refresh_token": "changeme"}, "content.credential_assignment", "changeme"),
        (
            {"client_secret": "${CLIENT_SECRET}"},
            "content.credential_assignment",
            "${CLIENT_SECRET}",
        ),
        ({"authorization": "Bearer REDACTED"}, "content.bearer_assignment", "REDACTED"),
        (
            {"ConnectionStrings": {"Default": "REDACTED"}},
            "content.connection_string_assignment",
            "REDACTED",
        ),
    ],
)
def test_windows_installer_verifier_rejects_placeholder_credential_assignments(
    tmp_path: Path,
    configuration: dict[str, object],
    expected_rule: str,
    sentinel: str,
) -> None:
    raw_name = "config/settings.json"
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            (raw_name, json.dumps(configuration).encode("utf-8")),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        raw_name,
        expected_rule,
        ordinal=2,
    )
    assert sentinel not in result.stderr


def test_windows_installer_verifier_rejects_malformed_zip(tmp_path: Path) -> None:
    result = _run_appended_payload_verifier(
        tmp_path,
        [],
        raw_payload=b"this-is-not-a-zip",
    )

    assert result.returncode != 0
    assert "zip-structure:end-of-central-directory" in result.stderr


def test_windows_installer_verifier_rejects_oversized_central_directory(
    tmp_path: Path,
) -> None:
    payload_path = tmp_path / "plain.zip"
    with zipfile.ZipFile(payload_path, "w") as archive:
        archive.writestr("Chummer.Avalonia.exe", b"placeholder")
    inconsistent_payload = _mutate_eocd_central_directory_size(
        payload_path.read_bytes(),
        16 * 1024 * 1024 + 1,
    )

    result = _run_appended_payload_verifier(
        tmp_path,
        [],
        raw_payload=inconsistent_payload,
    )

    assert result.returncode != 0
    assert "resource-limit:central-directory-bytes" in result.stderr


@pytest.mark.parametrize(
    ("unsafe_name", "expected_rule"),
    [
        ("../outside.txt", "path.relative"),
        ("/absolute.txt", "path.relative"),
        ("C:/outside.txt", "path.relative"),
        (r"folder\outside.txt", "path.forward_slash"),
        ("folder/control\x1f.txt", "path.ascii_printable"),
        ("folder/control\u0085.txt", "path.ascii_printable"),
        ("folder/café.txt", "path.ascii_printable"),
        ("folder/file:stream.txt", "path.windows_invalid_segment"),
        ("folder/file<name>.txt", "path.windows_invalid_segment"),
        ("folder/file>name.txt", "path.windows_invalid_segment"),
        ('folder/file"name.txt', "path.windows_invalid_segment"),
        ("folder/file|name.txt", "path.windows_invalid_segment"),
        ("folder/file?name.txt", "path.windows_invalid_segment"),
        ("folder/file*name.txt", "path.windows_invalid_segment"),
        ("folder/trailing.", "path.windows_invalid_segment"),
        ("folder/trailing ", "path.windows_invalid_segment"),
        ("CON", "path.windows_reserved_device"),
        ("folder/con.txt", "path.windows_reserved_device"),
        ("folder/PRN.log", "path.windows_reserved_device"),
        ("folder/AUX", "path.windows_reserved_device"),
        ("folder/NUL.json", "path.windows_reserved_device"),
        ("folder/COM1.txt", "path.windows_reserved_device"),
        ("folder/LPT9.bin", "path.windows_reserved_device"),
        ("folder/COM¹.txt", "path.ascii_printable"),
        (f"folder/{'a' * 256}.txt", "path.segment_length"),
        (
            "/".join(["a" * 220] * 5) + ".txt",
            "path.length",
        ),
    ],
)
def test_windows_installer_verifier_rejects_unsafe_zip_paths(
    tmp_path: Path,
    unsafe_name: str,
    expected_rule: str,
) -> None:
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            (unsafe_name, b"unsafe"),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        unsafe_name,
        expected_rule,
        ordinal=2,
    )


def test_windows_installer_verifier_never_leaks_attacker_controlled_entry_name(
    tmp_path: Path,
) -> None:
    raw_name = "payload/fakeBearerToken1234567890?.txt"
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            (raw_name, b"unsafe"),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        raw_name,
        "path.windows_invalid_segment",
        ordinal=2,
    )
    assert "fakeBearerToken1234567890" not in result.stderr


def test_windows_installer_verifier_rejects_symlink_entry(tmp_path: Path) -> None:
    link = zipfile.ZipInfo("linked-config")
    link.create_system = 3
    link.external_attr = (stat.S_IFLNK | 0o777) << 16

    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            (link, b"config/settings.json"),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        "linked-config",
        "entry.symlink",
        ordinal=2,
    )


@pytest.mark.parametrize(
    ("entry_names", "expected_rule"),
    [
        (("duplicate.txt", "duplicate.txt"), "path.duplicate"),
        (("Config/settings.json", "config/settings.json"), "path.portable_collision"),
    ],
)
def test_windows_installer_verifier_rejects_duplicate_and_case_colliding_entries(
    tmp_path: Path,
    entry_names: tuple[str, str],
    expected_rule: str,
) -> None:
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            (entry_names[0], b"first"),
            (entry_names[1], b"second"),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        entry_names[1],
        expected_rule,
        ordinal=3,
    )


def test_windows_installer_verifier_rejects_bzip2_payload_entry(tmp_path: Path) -> None:
    raw_name = "Chummer.Avalonia.exe"
    result = _run_appended_payload_verifier(
        tmp_path,
        [(raw_name, b"placeholder")],
        compression=zipfile.ZIP_BZIP2,
    )

    _assert_redacted_zip_entry_failure(
        result,
        raw_name,
        "entry.compression_method",
        ordinal=1,
    )


def test_windows_installer_verifier_rejects_local_only_encryption_flag(
    tmp_path: Path,
) -> None:
    raw_name = "Chummer.Avalonia.exe"
    payload_path = tmp_path / "plain.zip"
    with zipfile.ZipFile(payload_path, "w") as archive:
        archive.writestr(raw_name, b"placeholder")
    inconsistent_payload = _mutate_first_zip_local_header(
        payload_path.read_bytes(),
        add_flags=0x1,
    )

    result = _run_appended_payload_verifier(
        tmp_path,
        [],
        raw_payload=inconsistent_payload,
    )

    _assert_redacted_zip_entry_failure(
        result,
        raw_name,
        "entry.encrypted",
        ordinal=1,
    )


def test_windows_installer_verifier_rejects_local_and_central_flag_mismatch(
    tmp_path: Path,
) -> None:
    raw_name = "Chummer.Avalonia.exe"
    payload_path = tmp_path / "plain.zip"
    with zipfile.ZipFile(payload_path, "w") as archive:
        archive.writestr(raw_name, b"placeholder")
    inconsistent_payload = _mutate_first_zip_local_header(
        payload_path.read_bytes(),
        add_flags=0x8,
    )

    result = _run_appended_payload_verifier(
        tmp_path,
        [],
        raw_payload=inconsistent_payload,
    )

    _assert_redacted_zip_entry_failure(
        result,
        raw_name,
        "entry.local_flags",
        ordinal=1,
    )


def test_windows_installer_verifier_rejects_local_and_central_method_mismatch(
    tmp_path: Path,
) -> None:
    raw_name = "Chummer.Avalonia.exe"
    payload_path = tmp_path / "plain.zip"
    with zipfile.ZipFile(payload_path, "w", compression=zipfile.ZIP_STORED) as archive:
        archive.writestr(raw_name, b"placeholder")
    inconsistent_payload = _mutate_first_zip_local_header(
        payload_path.read_bytes(),
        compression_method=zipfile.ZIP_DEFLATED,
    )

    result = _run_appended_payload_verifier(
        tmp_path,
        [],
        raw_payload=inconsistent_payload,
    )

    _assert_redacted_zip_entry_failure(
        result,
        raw_name,
        "entry.local_compression_method",
        ordinal=1,
    )


def test_windows_installer_verifier_rejects_local_and_central_name_mismatch(
    tmp_path: Path,
) -> None:
    raw_name = "Chummer.Avalonia.exe"
    local_name = "Bhummer.Avalonia.exe"
    payload_path = tmp_path / "plain.zip"
    with zipfile.ZipFile(payload_path, "w") as archive:
        archive.writestr(raw_name, b"placeholder")
    inconsistent_payload = _mutate_first_zip_local_header(
        payload_path.read_bytes(),
        replacement_name=local_name,
    )

    result = _run_appended_payload_verifier(
        tmp_path,
        [],
        raw_payload=inconsistent_payload,
    )

    _assert_redacted_zip_entry_failure(
        result,
        raw_name,
        "entry.local_filename",
        ordinal=1,
    )
    assert local_name not in result.stderr


def test_windows_installer_verifier_rejects_out_of_bounds_local_header_lengths(
    tmp_path: Path,
) -> None:
    raw_name = "Chummer.Avalonia.exe"
    payload_path = tmp_path / "plain.zip"
    with zipfile.ZipFile(payload_path, "w") as archive:
        archive.writestr(raw_name, b"placeholder")
    inconsistent_payload = _mutate_first_zip_local_header(
        payload_path.read_bytes(),
        filename_length=0xFFFF,
    )

    result = _run_appended_payload_verifier(
        tmp_path,
        [],
        raw_payload=inconsistent_payload,
    )

    _assert_redacted_zip_entry_failure(
        result,
        raw_name,
        "entry.local_header_bounds",
        ordinal=1,
    )


def test_windows_installer_verifier_rejects_more_than_2048_entries_by_default(
    tmp_path: Path,
) -> None:
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            *((f"payload/{index:04d}.bin", b"x") for index in range(2048)),
        ],
    )

    assert result.returncode != 0
    assert "resource-limit:entry-count (2049 > 2048)" in result.stderr


def test_windows_installer_verifier_rejects_encrypted_entry(tmp_path: Path) -> None:
    payload_path = tmp_path / "plain.zip"
    with zipfile.ZipFile(payload_path, "w") as archive:
        archive.writestr("Chummer.Avalonia.exe", b"placeholder")
    encrypted_payload = _mark_first_zip_entry_encrypted(payload_path.read_bytes())

    result = _run_appended_payload_verifier(
        tmp_path,
        [],
        raw_payload=encrypted_payload,
    )

    _assert_redacted_zip_entry_failure(
        result,
        "Chummer.Avalonia.exe",
        "entry.encrypted",
        ordinal=1,
    )


def test_windows_installer_verifier_rejects_corrupt_entry_crc(tmp_path: Path) -> None:
    payload_path = tmp_path / "plain.zip"
    with zipfile.ZipFile(payload_path, "w") as archive:
        archive.writestr("Chummer.Avalonia.exe", b"placeholder")
    corrupt_payload = _corrupt_first_zip_entry_data(payload_path.read_bytes())

    result = _run_appended_payload_verifier(
        tmp_path,
        [],
        raw_payload=corrupt_payload,
    )

    _assert_redacted_zip_entry_failure(
        result,
        "Chummer.Avalonia.exe",
        "entry.integrity",
        ordinal=1,
    )


@pytest.mark.parametrize(
    ("sensitive_name", "expected_rule"),
    [
        ("config/.env.production", "name.sensitive"),
        ("keys/id_rsa", "name.sensitive"),
        ("keys/release-signing.pfx", "name.sensitive"),
        ("config/service-account.json", "name.sensitive"),
        ("config/client-secrets.json", "name.sensitive"),
    ],
)
def test_windows_installer_verifier_rejects_sensitive_entry_names(
    tmp_path: Path,
    sensitive_name: str,
    expected_rule: str,
) -> None:
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            (sensitive_name, b"not-a-real-secret"),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        sensitive_name,
        expected_rule,
        ordinal=2,
    )
    assert "not-a-real-secret" not in result.stderr


@pytest.mark.parametrize(
    "private_key_marker",
    [
        "-----BEGIN " + "RSA PRIVATE KEY-----",
        "-----BEGIN " + "ENCRYPTED PRIVATE KEY-----",
        "-----BEGIN " + "PGP PRIVATE KEY BLOCK-----",
    ],
)
def test_windows_installer_verifier_rejects_private_key_markers_without_leaking_content(
    tmp_path: Path,
    private_key_marker: str,
) -> None:
    private_material = f"{private_key_marker}\nfake-private-material\n".encode("utf-8")
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            ("keys/signing.pem", private_material),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        "keys/signing.pem",
        "content.private_key_marker",
        ordinal=2,
    )
    assert "fake-private-material" not in result.stderr


@pytest.mark.parametrize(
    ("assignment", "expected_rule", "secret_value"),
    [
        (
            "Authorization: " + "Bearer fakeBearerToken1234567890",
            "content.bearer_assignment",
            "fakeBearerToken1234567890",
        ),
        (
            "Authorization: " + "Bearer REDACTED",
            "content.bearer_assignment",
            "REDACTED",
        ),
        (
            "bearer_token=fakeBearerAssignment1234567890",
            "content.credential_assignment",
            "fakeBearerAssignment1234567890",
        ),
        (
            "refresh_token=fakeRefreshToken1234567890",
            "content.credential_assignment",
            "fakeRefreshToken1234567890",
        ),
        (
            "access-token: fakeAccessToken1234567890",
            "content.credential_assignment",
            "fakeAccessToken1234567890",
        ),
        (
            '<add connectionString="Server=db;User Id=runner;Password=fake-password" />',
            "content.connection_string_assignment",
            "fake-password",
        ),
        (
            '{"client_secret":"fakeClientSecret1234567890"}',
            "content.credential_assignment",
            "fakeClientSecret1234567890",
        ),
        (
            "client_secret=placeholder",
            "content.credential_assignment",
            "placeholder",
        ),
        (
            '<add connectionString="REDACTED" />',
            "content.connection_string_assignment",
            "REDACTED",
        ),
    ],
)
def test_windows_installer_verifier_rejects_secret_assignments_without_leaking_values(
    tmp_path: Path,
    assignment: str,
    expected_rule: str,
    secret_value: str,
) -> None:
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            ("config/runtime.conf", assignment.encode("utf-8")),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        "config/runtime.conf",
        expected_rule,
        ordinal=2,
    )
    assert secret_value not in result.stderr


@pytest.mark.parametrize(
    "configuration",
    [
        {"ConnectionStrings": {}},
        {"ConnectionStrings": None},
        {"client_secret": ""},
        {"client_secret": None},
        {"access_token": []},
        {"private_key": {}},
    ],
)
def test_windows_installer_verifier_accepts_empty_sensitive_json_values(
    tmp_path: Path,
    configuration: dict[str, object],
) -> None:
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            ("config/settings.json", json.dumps(configuration).encode("utf-8")),
        ],
    )

    assert result.returncode == 0, result.stderr
    assert "windows_installer_payload_gate:ok checked=1" in result.stdout


@pytest.mark.parametrize(
    ("configuration", "expected_rule", "secret_value"),
    [
        (
            {"ConnectionStrings": {"Default": "nested-connection-value"}},
            "content.connection_string_assignment",
            "nested-connection-value",
        ),
        (
            {"wrapper": {"client_secret": {"value": "nested-client-value"}}},
            "content.credential_assignment",
            "nested-client-value",
        ),
        (
            {"access_token": ["nested-access-value"]},
            "content.credential_assignment",
            "nested-access-value",
        ),
    ],
)
def test_windows_installer_verifier_rejects_nested_non_empty_sensitive_json_values(
    tmp_path: Path,
    configuration: dict[str, object],
    expected_rule: str,
    secret_value: str,
) -> None:
    raw_name = "config/settings.json"
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            (raw_name, json.dumps(configuration).encode("utf-8")),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        raw_name,
        expected_rule,
        ordinal=2,
    )
    assert secret_value not in result.stderr


def test_windows_installer_verifier_rejects_oauth_client_secret_embedded_in_binary_entry(
    tmp_path: Path,
) -> None:
    fake_secret = "fakeEmbeddedClientSecret1234567890"
    binary_with_embedded_json = (
        b"MZ\x00\x00binary-prefix\n"
        + json.dumps(
            {"installed": {"client_id": "fake-id", "client_secret": fake_secret}}
        ).encode("utf-8")
        + b"\x00binary-suffix"
    )
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            ("Chummer.Legacy.dll", binary_with_embedded_json),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        "Chummer.Legacy.dll",
        "content.credential_assignment",
        ordinal=2,
    )
    assert fake_secret not in result.stderr


@pytest.mark.parametrize(
    ("content_kind", "raw_name", "expected_rule"),
    [
        (
            "text",
            "docs/oversized.txt",
            "content.text_inspection_size",
        ),
        (
            "json",
            "config/oversized.json",
            "content.json_inspection_size",
        ),
    ],
)
def test_windows_installer_verifier_rejects_oversized_inspectable_content(
    tmp_path: Path,
    content_kind: str,
    raw_name: str,
    expected_rule: str,
) -> None:
    contents = (
        b"A" * (MAX_PAYLOAD_ZIP_INSPECTABLE_CONTENT_BYTES + 1)
        if content_kind == "text"
        else b'{"value":"'
        + b"A" * MAX_PAYLOAD_ZIP_INSPECTABLE_CONTENT_BYTES
        + b'"}'
    )
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            (raw_name, contents),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        raw_name,
        expected_rule,
        ordinal=2,
    )


def test_windows_installer_verifier_streams_oversized_binary_content(
    tmp_path: Path,
) -> None:
    binary_contents = (
        b"MZ" + b"\x00" * (MAX_PAYLOAD_ZIP_INSPECTABLE_CONTENT_BYTES - 1)
    )
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            ("assets/large-binary.dll", binary_contents),
        ],
    )

    assert result.returncode == 0, result.stderr
    assert "windows_installer_payload_gate:ok checked=1" in result.stdout


def test_windows_installer_verifier_scans_complete_oversized_binary_content(
    tmp_path: Path,
) -> None:
    sentinel = b"client_secret=X"
    binary_contents = (
        b"MZ"
        + b"\x00" * MAX_PAYLOAD_ZIP_INSPECTABLE_CONTENT_BYTES
        + sentinel
    )
    raw_name = "assets/large-binary.dll"
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            (raw_name, binary_contents),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        raw_name,
        "content.credential_assignment",
        ordinal=2,
    )
    assert sentinel.decode("ascii") not in result.stderr


def test_windows_installer_verifier_rejects_structural_google_service_account_json_regardless_of_name(
    tmp_path: Path,
) -> None:
    service_account = {
        "type": "service_account",
        "project_id": "example-project",
        "private_key_id": "fake-key-id",
        "private_key": "not-a-real-key",
        "client_email": "service@example.invalid",
        "token_uri": "https://oauth2.example.invalid/token",
    }
    result = _run_appended_payload_verifier(
        tmp_path,
        [
            ("Chummer.Avalonia.exe", b"placeholder"),
            ("assets/opaque.bin", json.dumps(service_account).encode("utf-8")),
        ],
    )

    _assert_redacted_zip_entry_failure(
        result,
        "assets/opaque.bin",
        "content.google_service_account_json",
        ordinal=2,
    )
    assert "fake-key-id" not in result.stderr
    assert "service@example.invalid" not in result.stderr


@pytest.mark.parametrize(
    ("environment", "entries", "compression", "expected_rule"),
    [
        (
            {"CHUMMER_WINDOWS_PAYLOAD_ZIP_MAX_ENTRIES": "2"},
            [("one.txt", b"1"), ("two.txt", b"2")],
            zipfile.ZIP_STORED,
            "resource-limit:entry-count",
        ),
        (
            {"CHUMMER_WINDOWS_PAYLOAD_ZIP_MAX_ENTRY_BYTES": "16"},
            [("large.bin", b"x" * 17)],
            zipfile.ZIP_STORED,
            "entry.decompressed_size",
        ),
        (
            {"CHUMMER_WINDOWS_PAYLOAD_ZIP_MAX_TOTAL_BYTES": "16"},
            [("six.bin", b"123456")],
            zipfile.ZIP_STORED,
            "resource-limit:total-bytes",
        ),
        (
            {"CHUMMER_WINDOWS_PAYLOAD_ZIP_MAX_COMPRESSION_RATIO": "2"},
            [("compressed.bin", b"a" * 1024)],
            zipfile.ZIP_DEFLATED,
            "entry.compression_ratio",
        ),
        (
            {"CHUMMER_WINDOWS_PAYLOAD_ZIP_MAX_ARCHIVE_BYTES": "64"},
            [],
            zipfile.ZIP_STORED,
            "resource-limit:archive-bytes",
        ),
    ],
)
def test_windows_installer_verifier_enforces_bounded_zip_resource_limits(
    tmp_path: Path,
    environment: dict[str, str],
    entries: list[tuple[str, bytes]],
    compression: int,
    expected_rule: str,
) -> None:
    result = _run_appended_payload_verifier(
        tmp_path,
        [("Chummer.Avalonia.exe", b"placeholder"), *entries],
        compression=compression,
        environment=environment,
    )

    assert result.returncode != 0
    assert expected_rule in result.stderr


def test_windows_installer_verifier_rejects_missing_payload(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)

    result = subprocess.run(
        ["python3", str(VERIFY_SCRIPT), "--files-dir", str(files_dir)],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "no appended payload and no bootstrap sidecar" in result.stderr


def test_publish_download_bundle_fails_before_promotion_when_windows_payload_is_missing(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name=installer_path.name)

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "windows_installer_payload_gate:fail" in result.stderr
    assert "no appended payload and no bootstrap sidecar" in result.stderr


def test_publish_download_bundle_fails_when_root_installer_has_no_matching_payload_sidecar(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = bundle_dir / "chummer-avalonia-win-x64-installer.exe"
    installer_path.write_bytes(b"installer-stub" * 200)
    (files_dir / "chummer-avalonia-win-x64.zip").write_bytes(b"portable-placeholder")
    _write_bundle_manifest(bundle_dir / "releases.json", installer_name=installer_path.name)

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "windows_installer_payload_gate:fail" in result.stderr
    assert "no appended payload and no bootstrap sidecar" in result.stderr


def test_publish_download_bundle_promotes_bootstrap_payload_zip_with_installer(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    payload_sidecar = files_dir / "chummer-avalonia-win-x64-payload.zip.json"
    payload_sidecar.write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    linux_path = files_dir / "chummer-avalonia-linux-x64-installer.deb"
    linux_path.write_bytes(b"linux-installer-placeholder")
    linux_sha256 = hashlib.sha256(linux_path.read_bytes()).hexdigest()
    macos_path = files_dir / "chummer-avalonia-osx-arm64-installer.dmg"
    macos_path.write_bytes(b"macos-installer-placeholder")
    macos_sha256 = hashlib.sha256(macos_path.read_bytes()).hexdigest()
    manifest_payload = json.loads((bundle_dir / "releases.json").read_text(encoding="utf-8"))
    manifest_payload["downloads"].append(
        {
            "artifactId": "avalonia-linux-x64-installer",
            "fileName": linux_path.name,
            "url": f"https://example.invalid/downloads/files/{linux_path.name}",
            "sha256": linux_sha256,
            "sizeBytes": linux_path.stat().st_size,
            "kind": "installer",
            "platform": "linux",
            "head": "avalonia",
            "rid": "linux-x64",
        }
    )
    manifest_payload["downloads"].append(
        {
            "artifactId": "avalonia-osx-arm64-installer",
            "fileName": macos_path.name,
            "url": f"https://example.invalid/downloads/files/{macos_path.name}",
            "sha256": macos_sha256,
            "sizeBytes": macos_path.stat().st_size,
            "kind": "installer",
            "platform": "macos",
            "head": "avalonia",
            "rid": "osx-arm64",
        }
    )
    (bundle_dir / "releases.json").write_text(json.dumps(manifest_payload, indent=2) + "\n", encoding="utf-8")
    _write_test_mac_build_provenance(
        bundle_dir,
        artifact_path=macos_path,
    )
    progress_screenshot = tmp_path / "windows-installer-progress.png"
    completion_screenshot = tmp_path / "windows-installer-completion.png"
    progress_screenshot.write_bytes(b"progress-image")
    completion_screenshot.write_bytes(b"completion-image")
    release_proof_path = tmp_path / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    release_proof_path.write_text(
        json.dumps(
            {
                "contractName": "chummer6-hub.local_release_proof",
                "status": "passed",
                "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "baseUrl": "https://example.invalid",
                "journeysPassed": [
                    "install_claim_restore_continue",
                    "build_explain_publish",
                    "campaign_session_recover_recap",
                    "report_cluster_release_notify",
                    "organize_community_and_close_loop",
                ],
                "proofRoutes": [
                    "/downloads/install/avalonia-linux-x64-installer",
                    "/home/access",
                    "/home/work",
                    "/account/access",
                        "/account/work",
                        "/account/roster",
                        "/account/support",
                        "/contact",
                    "/downloads",
                    "/downloads/install/avalonia-osx-arm64-installer",
                    "/downloads/install/avalonia-win-x64-installer",
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    startup_smoke_dir = bundle_dir / "startup-smoke"
    startup_smoke_dir.mkdir()
    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    visual_proof_path = tmp_path / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
    visual_proof_path.write_text(
        json.dumps(
            {
                "contract_name": "chummer6-ui.windows_installer_visual_proof",
                "contractName": "chummer6-ui.windows_installer_visual_proof",
                "status": "pass",
                "generated_at": recorded_at,
                "generatedAt": recorded_at,
                "recordedAtUtc": recorded_at,
                "channelId": "preview",
                "releaseVersion": "run-test",
                "version": "run-test",
                "headId": "avalonia",
                "head": "avalonia",
                "platform": "windows",
                "rid": "win-x64",
                "artifactDigest": f"sha256:{installer_sha256}",
                "screenshots": [
                    {
                        "role": "progress",
                        "path": str(progress_screenshot),
                        "sha256": hashlib.sha256(progress_screenshot.read_bytes()).hexdigest(),
                    },
                    {
                        "role": "completion",
                        "path": str(completion_screenshot),
                        "sha256": hashlib.sha256(completion_screenshot.read_bytes()).hexdigest(),
                    },
                ],
                "readabilityReview": {"status": "pass"},
                "contrastReview": {"status": "pass"},
                "clippingReview": {"status": "pass"},
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "windows",
                "arch": "x64",
                "rid": "win-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                    "hostClass": "wine64-linux-x64-container",
                    "operatingSystem": "Microsoft Windows 10.0.19043",
                    "executionEnvironment": "wine_compatibility",
                    "verificationScope": "windows_compatibility_startup",
                    "nativeHostEvidence": {
                        "contractName": "chummer6-ui.native_windows_host_evidence",
                        "status": "not_native",
                        "isNativeWindows": False,
                        "hostPlatform": "linux",
                        "hostKernel": "Linux",
                        "runner": "wine64",
                        "evidenceSource": "wine_runner_selection",
                    },
                "artifactDigest": f"sha256:{installer_sha256}",
                "artifactSha256": installer_sha256,
                "artifactFileName": installer_path.name,
                "fileName": installer_path.name,
                "artifactRelativePath": f"files/{installer_path.name}",
                "bootstrapPayloadAcquisitionMode": "download",
                "bootstrapPayloadFileName": payload_path.name,
                "bootstrapPayloadSha256": payload_sha256,
                "bootstrapPayloadSizeBytes": len(payload_bytes),
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                r"Bootstrap temp root: C:\users\tibor\Temp\Chummer6\installer-temp",
                rf"Payload download target: C:\users\tibor\Temp\Chummer6\installer-temp\{payload_path.name}",
                "Downloading application files",
                "Downloading application files - 50% - 24.5 / 49.0 MiB - 4.0 MiB/s",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-linux-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "linux",
                "arch": "x64",
                "rid": "linux-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "linux-x64-container",
                "operatingSystem": "Linux 6.0.0",
                "artifactDigest": f"sha256:{linux_sha256}",
                "artifactSha256": linux_sha256,
                "artifactFileName": linux_path.name,
                "fileName": linux_path.name,
                "artifactRelativePath": f"files/{linux_path.name}",
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-osx-arm64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "macos",
                "arch": "arm64",
                "rid": "osx-arm64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "macos-arm64-host",
                "operatingSystem": "macOS 15.0",
                "artifactDigest": f"sha256:{macos_sha256}",
                "artifactSha256": macos_sha256,
                "artifactFileName": macos_path.name,
                "fileName": macos_path.name,
                "artifactRelativePath": f"files/{macos_path.name}",
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
            CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="0",
            CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true",
            CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH=str(visual_proof_path),
            RELEASE_PROOF_PATH=str(release_proof_path),
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert (deploy_dir / "files" / installer_path.name).is_file()
    assert (deploy_dir / "files" / payload_path.name).is_file()
    assert (deploy_dir / "files" / payload_sidecar.name).is_file()
    assert (deploy_dir / "files" / macos_path.name).is_file()
    release_channel = json.loads(
        (deploy_dir / "RELEASE_CHANNEL.generated.json").read_text(encoding="utf-8")
    )
    assert any(
        artifact.get("artifactId") == "avalonia-osx-arm64-installer"
        for artifact in release_channel["artifacts"]
    )


def test_stable_macos_promotion_stays_blocked_without_signing_and_notarization(
    tmp_path: Path,
) -> None:
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    startup_smoke_dir = tmp_path / "startup-smoke"
    evidence_path = tmp_path / "public-promotion.json"
    startup_smoke_dir.mkdir()
    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    artifact_sha256 = "abc123"
    file_name = "chummer-avalonia-osx-arm64-installer.dmg"
    manifest_path.write_text(
        json.dumps(
            {
                "version": "run-stable-test",
                "channel": "public_stable",
                "artifacts": [
                    {
                        "artifactId": "avalonia-osx-arm64-installer",
                        "fileName": file_name,
                        "platform": "macos",
                        "head": "avalonia",
                        "rid": "osx-arm64",
                        "arch": "arm64",
                        "sha256": artifact_sha256,
                        "sizeBytes": 1,
                        "kind": "installer",
                    }
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-osx-arm64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "macos",
                "arch": "arm64",
                "rid": "osx-arm64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "macos-arm64-host",
                "operatingSystem": "macOS 15.0",
                "artifactDigest": f"sha256:{artifact_sha256}",
                "artifactSha256": artifact_sha256,
                "artifactFileName": file_name,
                "fileName": file_name,
                "releaseVersion": "run-stable-test",
                "channel": "public_stable",
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            "python3",
            str(REPO_ROOT / "scripts" / "generate-public-promotion-evidence.py"),
            "--manifest",
            str(manifest_path),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
            "--output",
            str(evidence_path),
            "--channel",
            "public_stable",
            "--generated-at",
            recorded_at,
        ],
        text=True,
        capture_output=True,
        check=False,
        env={"PATH": "/usr/bin:/bin"},
    )

    assert result.returncode == 0, result.stderr
    evidence = json.loads(evidence_path.read_text(encoding="utf-8"))
    artifact = evidence["artifacts"][0]
    assert artifact["startupSmokeStatus"] == "pass"
    assert artifact["signingStatus"] == "fail"
    assert artifact["notarizationStatus"] == "fail"
    assert artifact["promotionStatus"] == "fail"


def test_publish_download_bundle_refreshes_windows_visual_proof_handoff_before_exit_gate_failure(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    payload_sidecar = files_dir / "chummer-avalonia-win-x64-payload.zip.json"
    payload_sidecar.write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    linux_path = files_dir / "chummer-avalonia-linux-x64-installer.deb"
    linux_path.write_bytes(b"linux-installer-placeholder")
    linux_sha256 = hashlib.sha256(linux_path.read_bytes()).hexdigest()
    manifest_payload = json.loads((bundle_dir / "releases.json").read_text(encoding="utf-8"))
    manifest_payload["downloads"].append(
        {
            "artifactId": "avalonia-linux-x64-installer",
            "fileName": linux_path.name,
            "url": f"https://example.invalid/downloads/files/{linux_path.name}",
            "sha256": linux_sha256,
            "sizeBytes": linux_path.stat().st_size,
            "kind": "installer",
            "platform": "linux",
            "head": "avalonia",
            "rid": "linux-x64",
        }
    )
    (bundle_dir / "releases.json").write_text(json.dumps(manifest_payload, indent=2) + "\n", encoding="utf-8")
    macos_path, macos_sha256 = _append_plain_desktop_installer(
        bundle_dir,
        platform="macos",
    )
    release_proof_path = tmp_path / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    release_proof_path.write_text(
        json.dumps(
            {
                "contractName": "chummer6-hub.local_release_proof",
                "status": "passed",
                "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "baseUrl": "https://example.invalid",
                "journeysPassed": [
                    "install_claim_restore_continue",
                    "build_explain_publish",
                    "campaign_session_recover_recap",
                    "report_cluster_release_notify",
                    "organize_community_and_close_loop",
                ],
                "proofRoutes": [
                    "/downloads/install/avalonia-linux-x64-installer",
                    "/home/access",
                    "/home/work",
                    "/account/access",
                        "/account/work",
                        "/account/roster",
                        "/account/support",
                        "/contact",
                    "/downloads",
                    "/downloads/install/avalonia-osx-arm64-installer",
                    "/downloads/install/avalonia-win-x64-installer",
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    startup_smoke_dir = bundle_dir / "startup-smoke"
    startup_smoke_dir.mkdir()
    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    (startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "windows",
                "arch": "x64",
                "rid": "win-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                    "hostClass": "wine64-linux-x64-container",
                    "operatingSystem": "Microsoft Windows 10.0.19043",
                    "executionEnvironment": "wine_compatibility",
                    "verificationScope": "windows_compatibility_startup",
                    "nativeHostEvidence": {
                        "contractName": "chummer6-ui.native_windows_host_evidence",
                        "status": "not_native",
                        "isNativeWindows": False,
                        "hostPlatform": "linux",
                        "hostKernel": "Linux",
                        "runner": "wine64",
                        "evidenceSource": "wine_runner_selection",
                    },
                "artifactDigest": f"sha256:{installer_sha256}",
                "artifactSha256": installer_sha256,
                "artifactFileName": installer_path.name,
                "fileName": installer_path.name,
                "artifactRelativePath": f"files/{installer_path.name}",
                "bootstrapPayloadAcquisitionMode": "download",
                "bootstrapPayloadFileName": payload_path.name,
                "bootstrapPayloadSha256": payload_sha256,
                "bootstrapPayloadSizeBytes": len(payload_bytes),
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                r"Bootstrap temp root: C:\users\tibor\Temp\Chummer6\installer-temp",
                rf"Payload download target: C:\users\tibor\Temp\Chummer6\installer-temp\{payload_path.name}",
                "Downloading application files",
                "Downloading application files - 50% - 24.5 / 49.0 MiB - 4.0 MiB/s",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-linux-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "linux",
                "arch": "x64",
                "rid": "linux-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "linux-x64-container",
                "operatingSystem": "Linux 6.0.0",
                "artifactDigest": f"sha256:{linux_sha256}",
                "artifactSha256": linux_sha256,
                "artifactFileName": linux_path.name,
                "fileName": linux_path.name,
                "artifactRelativePath": f"files/{linux_path.name}",
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_plain_installer_startup_smoke(
        bundle_dir,
        artifact_path=macos_path,
        artifact_sha256=macos_sha256,
        platform="macos",
        recorded_at=recorded_at,
    )

    handoff_stub = tmp_path / "handoff_stub.py"
    handoff_stub.write_text(
        "\n".join(
            [
                "from __future__ import annotations",
                "import json, sys",
                "from pathlib import Path",
                "root = Path(sys.argv[1])",
                "handoff_path = root / 'WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json'",
                "payload = {",
                "  'status': 'ready_for_windows_host',",
                "  'summary': 'Windows desktop exit gate failed: Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.',",
                "  'json_path': str(handoff_path),",
                "  'next_actions': ['Run the stage-local Windows visual capture lane.']",
                "}",
                "handoff_path.write_text(json.dumps(payload, indent=2) + '\\n', encoding='utf-8')",
                "(root / 'RELEASE_BUILD_HANDOFF.generated.json').write_text(json.dumps({'windows_visual_proof_handoff': payload}, indent=2) + '\\n', encoding='utf-8')",
            ]
        )
        + "\n",
        encoding="utf-8",
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
            CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="0",
            CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true",
            CHUMMER_RELEASE_BUILD_HANDOFF_SCRIPT_PATH=str(handoff_stub),
            RELEASE_PROOF_PATH=str(release_proof_path),
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Windows visual proof handoff:" in result.stderr
    assert str(bundle_dir / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json") in result.stderr
    assert not deploy_dir.exists()
    assert "Windows visual proof status: ready_for_windows_host" in result.stderr
    assert "Windows visual proof next action 1: Run the stage-local Windows visual capture lane." in result.stderr


def test_stable_publish_download_bundle_does_not_honor_forced_preview_visual_handoff_override(tmp_path: Path) -> None:
    stable_repo_root = Path("/docker/chummercomplete/chummer6-ui")
    stable_publish_script = stable_repo_root / "scripts" / "publish-download-bundle.sh"

    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-stable-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    linux_path = files_dir / "chummer-avalonia-linux-x64-installer.deb"
    linux_path.write_bytes(b"linux-installer-placeholder")
    linux_sha256 = hashlib.sha256(linux_path.read_bytes()).hexdigest()
    manifest_payload = json.loads((bundle_dir / "releases.json").read_text(encoding="utf-8"))
    manifest_payload["downloads"].append(
        {
            "artifactId": "avalonia-linux-x64-installer",
            "fileName": linux_path.name,
            "url": f"https://example.invalid/downloads/files/{linux_path.name}",
            "sha256": linux_sha256,
            "sizeBytes": linux_path.stat().st_size,
            "kind": "installer",
            "platform": "linux",
            "head": "avalonia",
            "rid": "linux-x64",
        }
    )
    (bundle_dir / "releases.json").write_text(json.dumps(manifest_payload, indent=2) + "\n", encoding="utf-8")
    macos_path, macos_sha256 = _append_plain_desktop_installer(
        bundle_dir,
        platform="macos",
    )

    release_proof_path = tmp_path / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    release_proof_path.write_text(
        json.dumps(
            {
                "contractName": "chummer6-hub.local_release_proof",
                "status": "passed",
                "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "baseUrl": "https://example.invalid",
                "journeysPassed": [
                    "install_claim_restore_continue",
                    "build_explain_publish",
                    "campaign_session_recover_recap",
                    "report_cluster_release_notify",
                    "organize_community_and_close_loop",
                ],
                "proofRoutes": [
                    "/downloads/install/avalonia-linux-x64-installer",
                    "/home/access",
                    "/home/work",
                    "/account/access",
                        "/account/work",
                        "/account/roster",
                        "/account/support",
                        "/contact",
                    "/downloads",
                    "/downloads/install/avalonia-osx-arm64-installer",
                    "/downloads/install/avalonia-win-x64-installer",
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    startup_smoke_dir = bundle_dir / "startup-smoke"
    startup_smoke_dir.mkdir()
    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    (startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "windows",
                "arch": "x64",
                "rid": "win-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                    "hostClass": "windows-x64-host",
                    "operatingSystem": "Windows 11",
                    "executionEnvironment": "native_windows",
                    "verificationScope": "native_windows_startup",
                    "nativeHostEvidence": {
                        "contractName": "chummer6-ui.native_windows_host_evidence",
                        "status": "verified",
                        "isNativeWindows": True,
                        "hostPlatform": "windows",
                        "hostKernel": "Windows_NT",
                        "runner": "powershell.exe",
                        "evidenceSource": "powershell_runtime_os_probe",
                    },
                "artifactDigest": f"sha256:{installer_sha256}",
                "artifactSha256": installer_sha256,
                "artifactFileName": installer_path.name,
                "fileName": installer_path.name,
                "artifactRelativePath": f"files/{installer_path.name}",
                "bootstrapPayloadAcquisitionMode": "download",
                "bootstrapPayloadFileName": payload_path.name,
                "bootstrapPayloadSha256": payload_sha256,
                "bootstrapPayloadSizeBytes": len(payload_bytes),
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                r"Bootstrap temp root: C:\users\tibor\Temp\Chummer6\installer-temp",
                rf"Payload download target: C:\users\tibor\Temp\Chummer6\installer-temp\{payload_path.name}",
                "Downloading application files",
                "Downloading application files - 50% - 24.5 / 49.0 MiB - 4.0 MiB/s",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-linux-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "linux",
                "arch": "x64",
                "rid": "linux-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "linux-x64-container",
                "operatingSystem": "Linux 6.0.0",
                "artifactDigest": f"sha256:{linux_sha256}",
                "artifactSha256": linux_sha256,
                "artifactFileName": linux_path.name,
                "fileName": linux_path.name,
                "artifactRelativePath": f"files/{linux_path.name}",
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_plain_installer_startup_smoke(
        bundle_dir,
        artifact_path=macos_path,
        artifact_sha256=macos_sha256,
        platform="macos",
        recorded_at=recorded_at,
    )

    handoff_stub = tmp_path / "preview_handoff_stub.py"
    handoff_stub.write_text(
        "\n".join(
            [
                "from __future__ import annotations",
                "import json, sys",
                "from pathlib import Path",
                "root = Path(sys.argv[1])",
                "handoff_path = root / 'WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json'",
                "visual = {",
                "  'status': 'ready_for_windows_host',",
                "  'summary': 'Windows desktop exit gate failed: Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.',",
                "  'json_path': str(handoff_path),",
                "  'next_actions': ['Run the stage-local Windows visual capture lane.'],",
                "  'only_blocker_is_visual_proof': True",
                "}",
                "release_handoff = {",
                "  'channel': 'preview',",
                "  'stage_proof_complete': False,",
                "  'blockers': ['Windows visual proof is still outstanding for the staged installer bytes.'],",
                "  'windows_visual_proof_handoff': visual",
                "}",
                "handoff_path.write_text(json.dumps(visual, indent=2) + '\\n', encoding='utf-8')",
                "(root / 'RELEASE_BUILD_HANDOFF.generated.json').write_text(json.dumps(release_handoff, indent=2) + '\\n', encoding='utf-8')",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    root_release_blockers = tmp_path / "RELEASE_BLOCKERS.generated.json"
    root_release_blockers.write_text(
        json.dumps(
            {
                "generated_at": _fresh_root_blocker_generated_at(),
                "blockers": [
                    {"blocker_id": "release_posture:non_flagship_channel"},
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(stable_publish_script), str(bundle_dir), str(deploy_dir)],
        cwd=stable_repo_root,
        env=_publish_env(
            tmp_path,
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
            CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="0",
            CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true",
            CHUMMER_RELEASE_BUILD_HANDOFF_SCRIPT_PATH=str(handoff_stub),
            RELEASE_PROOF_PATH=str(release_proof_path),
            CHUMMER_FORCE_NIGHTLY_PUBLISH="1",
            RELEASE_CHANNEL="public_stable",
            RELEASE_VERSION="run-stable-test",
            RELEASE_PUBLISHED_AT="2026-07-06T00:00:00Z",
            CHUMMER_ROOT_RELEASE_BLOCKERS_PATH=str(root_release_blockers),
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Forced preview nightly publication continuing with Windows visual proof handoff only" not in result.stderr
    assert "Published downloads shelf failed Windows desktop exit gate verification. Use the Windows visual proof handoff above." in result.stderr
    assert "Windows installer visual proof is missing" in result.stderr


def test_stable_publish_download_bundle_refuses_non_posture_root_blockers(tmp_path: Path) -> None:
    stable_repo_root = Path("/docker/chummercomplete/chummer6-ui")
    stable_publish_script = stable_repo_root / "scripts" / "publish-download-bundle.sh"

    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-stable-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    root_release_blockers = tmp_path / "RELEASE_BLOCKERS.generated.json"
    root_release_blockers.write_text(
        json.dumps(
            {
                "generated_at": _fresh_root_blocker_generated_at(),
                "blockers": [
                    {"blocker_id": "release_posture:non_flagship_channel"},
                    {"blocker_id": "release_truth:windows_installer_visual_audit"},
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(stable_publish_script), str(bundle_dir), str(deploy_dir)],
        cwd=stable_repo_root,
        env=_publish_env(
            tmp_path,
            RELEASE_CHANNEL="public_stable",
            RELEASE_VERSION="run-stable-test",
            RELEASE_PUBLISHED_AT="2026-07-06T00:00:00Z",
            CHUMMER_ROOT_RELEASE_BLOCKERS_PATH=str(root_release_blockers),
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Public stable publication is blocked by root release truth." in result.stderr
    assert "release_truth:windows_installer_visual_audit" in result.stderr
    assert "Windows visual proof handoff:" not in result.stderr
    assert "Published downloads shelf failed Windows desktop exit gate verification." not in result.stderr


def test_stable_publish_download_bundle_prefers_root_blocker_ids_over_non_root_blocker_noise(tmp_path: Path) -> None:
    stable_repo_root = Path("/docker/chummercomplete/chummer6-ui")
    stable_publish_script = stable_repo_root / "scripts" / "publish-download-bundle.sh"

    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-stable-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    linux_path, linux_sha256 = _append_plain_desktop_installer(
        bundle_dir,
        platform="linux",
    )
    macos_path, macos_sha256 = _append_plain_desktop_installer(
        bundle_dir,
        platform="macos",
    )

    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    startup_smoke_dir = bundle_dir / "startup-smoke"
    startup_smoke_dir.mkdir()
    (startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "windows",
                "arch": "x64",
                "rid": "win-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                    "hostClass": "windows-x64-host",
                    "operatingSystem": "Windows 11",
                    "executionEnvironment": "native_windows",
                    "verificationScope": "native_windows_startup",
                    "nativeHostEvidence": {
                        "contractName": "chummer6-ui.native_windows_host_evidence",
                        "status": "verified",
                        "isNativeWindows": True,
                        "hostPlatform": "windows",
                        "hostKernel": "Windows_NT",
                        "runner": "powershell.exe",
                        "evidenceSource": "powershell_runtime_os_probe",
                    },
                "artifactDigest": f"sha256:{installer_sha256}",
                "artifactSha256": installer_sha256,
                "artifactFileName": installer_path.name,
                "fileName": installer_path.name,
                "artifactRelativePath": f"files/{installer_path.name}",
                "bootstrapPayloadAcquisitionMode": "download",
                "bootstrapPayloadFileName": payload_path.name,
                "bootstrapPayloadSha256": payload_sha256,
                "bootstrapPayloadSizeBytes": len(payload_bytes),
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(
        "\n".join(
            [
                "# Chummer installer trace",
                r"Bootstrap temp root: C:\users\tibor\Temp\Chummer6\installer-temp",
                rf"Payload download target: C:\users\tibor\Temp\Chummer6\installer-temp\{payload_path.name}",
                "Downloading application files",
                "Downloading application files - 50% - 24.5 / 49.0 MiB - 4.0 MiB/s",
                "Verifying payload size",
                "Verifying payload checksum",
                "Extracting application files",
                "Install complete",
            ]
        )
        + "\n",
        encoding="utf-8",
    )
    _write_plain_installer_startup_smoke(
        bundle_dir,
        artifact_path=linux_path,
        artifact_sha256=linux_sha256,
        platform="linux",
        recorded_at=recorded_at,
    )
    _write_plain_installer_startup_smoke(
        bundle_dir,
        artifact_path=macos_path,
        artifact_sha256=macos_sha256,
        platform="macos",
        recorded_at=recorded_at,
    )
    release_proof_path = tmp_path / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    _write_release_proof_fixture(release_proof_path)

    root_release_blockers = tmp_path / "RELEASE_BLOCKERS.generated.json"
    root_release_blockers.write_text(
        json.dumps(
            {
                "generated_at": _fresh_root_blocker_generated_at(),
                "blockers": [
                    {"blocker_id": "release_posture:non_flagship_channel"},
                    {"blocker_id": "release_truth:windows_installer_visual_audit"},
                ],
                "root_blocker_ids": [
                    "release_posture:non_flagship_channel",
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(stable_publish_script), str(bundle_dir), str(deploy_dir)],
        cwd=stable_repo_root,
        env=_publish_env(
            tmp_path,
            RELEASE_CHANNEL="public_stable",
            RELEASE_VERSION="run-stable-test",
            RELEASE_PUBLISHED_AT="2026-07-06T00:00:00Z",
            CHUMMER_ROOT_RELEASE_BLOCKERS_PATH=str(root_release_blockers),
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
            CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true",
            RELEASE_PROOF_PATH=str(release_proof_path),
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Public stable publication is blocked by root release truth." not in result.stderr
    assert "release_truth:windows_installer_visual_audit" not in result.stderr
    assert "Published downloads shelf failed Windows desktop exit gate verification." in result.stderr
    assert not deploy_dir.exists()


def test_stable_publish_download_bundle_refuses_stale_root_blocker_truth(tmp_path: Path) -> None:
    stable_repo_root = Path("/docker/chummercomplete/chummer6-ui")
    stable_publish_script = stable_repo_root / "scripts" / "publish-download-bundle.sh"

    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-stable-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )

    root_release_blockers = tmp_path / "RELEASE_BLOCKERS.generated.json"
    root_release_blockers.write_text(
        json.dumps(
            {
                "generated_at": "2000-01-01T00:00:00Z",
                "root_blockers": [
                    {"blocker_id": "release_posture:non_flagship_channel"},
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(stable_publish_script), str(bundle_dir), str(deploy_dir)],
        cwd=stable_repo_root,
        env=_publish_env(
            tmp_path,
            RELEASE_CHANNEL="public_stable",
            RELEASE_VERSION="run-stable-test",
            RELEASE_PUBLISHED_AT="2026-07-06T00:00:00Z",
            CHUMMER_ROOT_RELEASE_BLOCKERS_PATH=str(root_release_blockers),
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Public stable publication requires fresh root release blocker truth." in result.stderr
    assert "generated_at=2000-01-01T00:00:00Z" in result.stderr
    assert "max_age_seconds=86400" in result.stderr
    assert "Public stable publication is blocked by root release truth." not in result.stderr
    assert "Windows visual proof handoff:" not in result.stderr
    assert "Published downloads shelf failed Windows desktop exit gate verification." not in result.stderr


def test_publish_download_bundle_fails_when_windows_bootstrap_receipt_payload_proof_is_wrong(tmp_path: Path) -> None:
    bundle_dir = tmp_path / "bundle"
    files_dir = bundle_dir / "files"
    files_dir.mkdir(parents=True)
    installer_path = files_dir / "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = _write_bootstrap_payload(payload_path)
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_path.name}"
    _write_bootstrap_installer(
        installer_path,
        payload_download_url=payload_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    installer_sha256 = hashlib.sha256(installer_path.read_bytes()).hexdigest()
    (files_dir / "chummer-avalonia-win-x64-payload.zip.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_path.name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_path.name,
                "releaseVersion": "run-test",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_bundle_manifest(
        bundle_dir / "releases.json",
        installer_name=installer_path.name,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_path.stat().st_size,
        payload_name=payload_path.name,
        payload_sha256=payload_sha256,
        payload_size_bytes=len(payload_bytes),
    )
    linux_path = files_dir / "chummer-avalonia-linux-x64-installer.deb"
    linux_path.write_bytes(b"linux-installer-placeholder")
    linux_sha256 = hashlib.sha256(linux_path.read_bytes()).hexdigest()
    manifest_payload = json.loads((bundle_dir / "releases.json").read_text(encoding="utf-8"))
    manifest_payload["downloads"].append(
        {
            "artifactId": "avalonia-linux-x64-installer",
            "fileName": linux_path.name,
            "url": f"https://example.invalid/downloads/files/{linux_path.name}",
            "sha256": linux_sha256,
            "sizeBytes": linux_path.stat().st_size,
            "kind": "installer",
            "platform": "linux",
            "head": "avalonia",
            "rid": "linux-x64",
        }
    )
    (bundle_dir / "releases.json").write_text(json.dumps(manifest_payload, indent=2) + "\n", encoding="utf-8")
    macos_path, macos_sha256 = _append_plain_desktop_installer(
        bundle_dir,
        platform="macos",
    )
    release_proof_path = tmp_path / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    release_proof_path.write_text(
        json.dumps(
            {
                "contractName": "chummer6-hub.local_release_proof",
                "status": "passed",
                "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
                "baseUrl": "https://example.invalid",
                "journeysPassed": [
                    "install_claim_restore_continue",
                    "build_explain_publish",
                    "campaign_session_recover_recap",
                    "report_cluster_release_notify",
                    "organize_community_and_close_loop",
                ],
                "proofRoutes": [
                    "/downloads/install/avalonia-linux-x64-installer",
                    "/home/access",
                    "/home/work",
                    "/account/access",
                        "/account/work",
                        "/account/roster",
                        "/account/support",
                        "/contact",
                    "/downloads",
                    "/downloads/install/avalonia-osx-arm64-installer",
                    "/downloads/install/avalonia-win-x64-installer",
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    startup_smoke_dir = bundle_dir / "startup-smoke"
    startup_smoke_dir.mkdir()
    recorded_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    (startup_smoke_dir / "startup-smoke-avalonia-win-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "windows",
                "arch": "x64",
                "rid": "win-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                    "hostClass": "wine64-linux-x64-container",
                    "operatingSystem": "Microsoft Windows 10.0.19043",
                    "executionEnvironment": "wine_compatibility",
                    "verificationScope": "windows_compatibility_startup",
                    "nativeHostEvidence": {
                        "contractName": "chummer6-ui.native_windows_host_evidence",
                        "status": "not_native",
                        "isNativeWindows": False,
                        "hostPlatform": "linux",
                        "hostKernel": "Linux",
                        "runner": "wine64",
                        "evidenceSource": "wine_runner_selection",
                    },
                "artifactDigest": f"sha256:{installer_sha256}",
                "artifactSha256": installer_sha256,
                "artifactFileName": installer_path.name,
                "fileName": installer_path.name,
                "artifactRelativePath": f"files/{installer_path.name}",
                "bootstrapPayloadAcquisitionMode": "download",
                "bootstrapPayloadFileName": payload_path.name,
                "bootstrapPayloadSha256": "wrong-payload-sha",
                "bootstrapPayloadSizeBytes": len(payload_bytes),
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    (startup_smoke_dir / "startup-smoke-avalonia-linux-x64.receipt.json").write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "linux",
                "arch": "x64",
                "rid": "linux-x64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "linux-x64-container",
                "operatingSystem": "Linux 6.0.0",
                "artifactDigest": f"sha256:{linux_sha256}",
                "artifactSha256": linux_sha256,
                "artifactFileName": linux_path.name,
                "fileName": linux_path.name,
                "artifactRelativePath": f"files/{linux_path.name}",
                "recordedAtUtc": recorded_at,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    _write_plain_installer_startup_smoke(
        bundle_dir,
        artifact_path=macos_path,
        artifact_sha256=macos_sha256,
        platform="macos",
        recorded_at=recorded_at,
    )
    deploy_dir = tmp_path / "deploy"
    result = subprocess.run(
        ["bash", str(PUBLISH_SCRIPT), str(bundle_dir), str(deploy_dir)],
        cwd=REPO_ROOT,
        env=_publish_env(
            tmp_path,
            CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS="false",
            CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="0",
            CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true",
            RELEASE_PROOF_PATH=str(release_proof_path),
        ),
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "Windows bootstrap installer startup-smoke receipt payloadSha256 mismatch" in result.stderr
