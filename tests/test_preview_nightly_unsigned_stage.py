from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import stat
import subprocess
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "preview_nightly_unsigned_stage.py"
SPEC = importlib.util.spec_from_file_location("preview_nightly_unsigned_stage", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
stage = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = stage
SPEC.loader.exec_module(stage)

VERSION = "run-20260722-150000"
PUBLISHED_AT = "2026-07-22T15:00:00Z"
SOURCE_SHA = "a" * 40


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, payload: object) -> None:
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def unsigned_pe(*, certificate_offset: int = 0, certificate_size: int = 0) -> bytes:
    value = bytearray(512)
    value[:2] = b"MZ"
    value[60:64] = (128).to_bytes(4, "little")
    value[128:132] = b"PE\x00\x00"
    value[132:134] = (0x8664).to_bytes(2, "little")
    value[148:150] = (0xE0).to_bytes(2, "little")
    optional = 152
    value[optional : optional + 2] = (0x10B).to_bytes(2, "little")
    security = optional + 128
    value[security : security + 4] = certificate_offset.to_bytes(4, "little")
    value[security + 4 : security + 8] = certificate_size.to_bytes(4, "little")
    return bytes(value)


def incumbent_row(
    *, platform: str, rid: str, name: str, sha256: str, size: int
) -> dict[str, object]:
    artifact_id = f"avalonia-{rid}-installer"
    label = "Linux X64" if platform == "linux" else "Windows X64"
    row: dict[str, object] = {
        "artifactId": artifact_id,
        "id": artifact_id,
        "fileName": name,
        "head": "avalonia",
        "kind": "installer",
        "platform": platform,
        "platformLabel": f"Avalonia Desktop {label} Installer",
        "rid": rid,
        "arch": "x64",
        "downloadUrl": f"https://chummer.run/downloads/files/{name}",
        "sha256": sha256,
        "sizeBytes": size,
        "compatibilityState": "compatible",
        "compatibilityReason": None,
        "installAccessClass": "open_public",
        "installerMode": "offline" if platform != "windows" else "bootstrap",
        "payloadFileName": None,
        "payloadDownloadUrl": None,
        "payloadSha256": None,
        "payloadSizeBytes": None,
    }
    return row


def download_row(row: dict[str, object]) -> dict[str, object]:
    return {
        "id": row["artifactId"],
        "artifactId": row["artifactId"],
        "platform": row["platformLabel"],
        "platformId": row["platform"],
        "url": row["downloadUrl"],
        "sha256": row["sha256"],
        "sizeBytes": row["sizeBytes"],
        "format": "deb" if row["platform"] == "linux" else "exe",
        "flavor": "installer",
        "kind": "installer",
        "head": "avalonia",
        "arch": "x64",
        "rid": row["rid"],
        "fileName": row["fileName"],
        "compatibilityState": "compatible",
        "compatibilityReason": None,
        "installerMode": row["installerMode"],
        "payloadFileName": row["payloadFileName"],
        "payloadDownloadUrl": row["payloadDownloadUrl"],
        "payloadSha256": row["payloadSha256"],
        "payloadSizeBytes": row["payloadSizeBytes"],
        "installAccessClass": "open_public",
    }


def blazor_row(*, kind: str, name: str, sha256: str, size: int) -> dict[str, object]:
    artifact_id = f"blazor-desktop-osx-arm64-{kind}"
    label_suffix = " Installer" if kind == "installer" else ""
    return {
        "artifactId": artifact_id,
        "head": "blazor-desktop",
        "rid": "osx-arm64",
        "platform": "macos",
        "arch": "arm64",
        "kind": kind,
        "fileName": name,
        "downloadUrl": f"/downloads/files/{name}",
        "sha256": sha256,
        "sizeBytes": size,
        "platformLabel": f"Blazor Desktop macOS ARM64{label_suffix}",
        "updateFeedUrl": None,
        "embeddedRuntimeBundleHeadId": None,
        "compatibilityState": "compatible",
        "installAccessClass": (
            "account_required" if kind == "installer" else "open_public"
        ),
        "channelId": "preview",
        "channel": "preview",
        "version": "incumbent-v1",
        "releaseVersion": "incumbent-v1",
        "generated_at": "2026-07-21T12:00:00Z",
        "generatedAt": "2026-07-21T12:00:00Z",
        "id": artifact_id,
    }


def blazor_download_row(row: dict[str, object]) -> dict[str, object]:
    return {
        "id": row["artifactId"],
        "platform": row["platformLabel"],
        "url": row["downloadUrl"],
        "sha256": row["sha256"],
        "sizeBytes": row["sizeBytes"],
        "head": "blazor-desktop",
        "platformId": "macos-arm64",
        "rid": None,
        "arch": "arm64",
        "kind": row["kind"],
        "fileName": row["fileName"],
        "installAccessClass": row["installAccessClass"],
        "platformLabel": None,
        "format": "dmg" if row["kind"] == "installer" else "tar.gz",
        "flavor": row["kind"],
        "channelId": "preview",
        "channel": "preview",
        "version": "incumbent-v1",
        "releaseVersion": "incumbent-v1",
        "compatibilityState": "compatible",
        "compatibilityReason": None,
        "artifactId": None,
        "installerMode": None,
        "payloadFileName": None,
        "payloadDownloadUrl": None,
        "payloadSha256": None,
        "payloadSizeBytes": None,
    }


def fixture(tmp_path: Path) -> dict[str, object]:
    incumbent = tmp_path / "incumbent"
    files = incumbent / "files"
    files.mkdir(parents=True)
    old_win = files / stage.INSTALLER_NAME
    old_payload = files / stage.PAYLOAD_NAME
    old_payload_sidecar = files / stage.PAYLOAD_SIDECAR_NAME
    linux = files / "chummer-avalonia-linux-x64-installer.deb"
    blazor_installer = files / "chummer-blazor-desktop-osx-arm64-installer.dmg"
    blazor_archive = files / "chummer-blazor-desktop-osx-arm64.tar.gz"
    old_win.write_bytes(b"old-windows")
    old_payload.write_bytes(b"old-payload")
    write_json(
        old_payload_sidecar,
        {
            "contractName": "chummer6-ui.windows_bootstrap_payload",
            "downloadUrl": f"https://chummer.run/downloads/files/{old_payload.name}",
            "fileName": old_payload.name,
            "installerFileName": old_win.name,
            "payloadAcquisitionMode": "download",
            "releaseVersion": "stale-incumbent",
            "sha256": digest(old_payload),
            "sizeBytes": old_payload.stat().st_size,
        },
    )
    old_payload_sidecar.chmod(0o600)
    linux.write_bytes(b"incumbent-linux")
    blazor_installer.write_bytes(b"incumbent-blazor-installer")
    blazor_archive.write_bytes(b"incumbent-blazor-archive")
    linux.chmod(0o640)
    ancillary = incumbent / "operator-note.txt"
    ancillary.write_bytes(b"retain-exactly")
    ancillary.chmod(0o600)
    ancillary_directory = incumbent / "operator-private"
    ancillary_directory.mkdir()
    ancillary_directory.chmod(0o710)
    (ancillary_directory / "note.txt").write_bytes(b"directory-mode-proof")
    rows = [
        incumbent_row(
            platform="windows",
            rid="win-x64",
            name=old_win.name,
            sha256=digest(old_win),
            size=old_win.stat().st_size,
        ),
        blazor_row(
            kind="installer",
            name=blazor_installer.name,
            sha256=digest(blazor_installer),
            size=blazor_installer.stat().st_size,
        ),
        blazor_row(
            kind="archive",
            name=blazor_archive.name,
            sha256=digest(blazor_archive),
            size=blazor_archive.stat().st_size,
        ),
        incumbent_row(
            platform="linux",
            rid="linux-x64",
            name=linux.name,
            sha256=digest(linux),
            size=linux.stat().st_size,
        ),
    ]
    rows[0].update(
        {
            "payloadAcquisitionMode": "download",
            "payloadFileName": old_payload.name,
            "payloadDownloadUrl": f"https://chummer.run/downloads/files/{old_payload.name}",
            "payloadSha256": digest(old_payload),
            "payloadSizeBytes": old_payload.stat().st_size,
        }
    )
    identity = {
        "channel": "preview",
        "channelId": "preview",
        "contract_name": "Chummer.Hub.Registry.Contracts",
        "contractName": "Chummer.Hub.Registry.Contracts",
        "generated_at": "2026-07-21T12:00:00Z",
        "generatedAt": "2026-07-21T12:00:00Z",
        "publishedAt": "2026-07-21T12:00:00Z",
        "projectionProfile": "v3_unsigned_windows_fresh_delta",
        "projectionStage": "registry_prepared_candidate",
        "registryCommit": "b" * 40,
        "registry_commit": "b" * 40,
        "releaseVersion": "incumbent-v1",
        "schemaVersion": 1,
        "version": "incumbent-v1",
        "desktopTupleCoverage": {
            "artifactCount": 99,
            "desktopArtifactCount": 99,
            "installerArtifactCount": 99,
            "installerTupleCount": 99,
            "promotedInstallerTupleCount": 99,
        },
        "registryBoundaryCoverage": {
            "compatibility": {
                "compatibleArtifactCount": 99,
                "unknownArtifactCount": 99,
            },
            "persistence": {"artifactCount": 99},
        },
    }
    write_json(incumbent / stage.CANONICAL_MANIFEST, {**identity, "artifacts": rows})
    write_json(
        incumbent / stage.COMPATIBILITY_MANIFEST,
        {
            **identity,
            "codeDeployCurrentShelfAuthority": {
                "authority": True,
                "contract": "stale-registry-authority",
            },
            "codeDeploymentAuthority": None,
            "downloads": [
                download_row(rows[0]),
                blazor_download_row(rows[1]),
                blazor_download_row(rows[2]),
                download_row(rows[3]),
            ],
            "releaseUploadAuthority": None,
        },
    )

    build = tmp_path / "build"
    build_files = build / "files"
    build_files.mkdir(parents=True)
    fresh_win = build_files / stage.INSTALLER_NAME
    fresh_payload = build_files / stage.PAYLOAD_NAME
    fresh_win.write_bytes(unsigned_pe())
    fresh_payload.write_bytes(b"fresh-payload")
    output = tmp_path / "output"
    args = argparse.Namespace(
        incumbent_root=incumbent,
        build_root=build,
        expected_version=VERSION,
        published_at=PUBLISHED_AT,
        source_sha=SOURCE_SHA,
        download_root="https://chummer.run/downloads/files",
        output=output,
    )
    return {
        "ancillary": ancillary,
        "ancillary_directory": ancillary_directory,
        "args": args,
        "build": build,
        "blazor_archive": blazor_archive,
        "blazor_installer": blazor_installer,
        "fresh_payload": fresh_payload,
        "fresh_win": fresh_win,
        "incumbent": incumbent,
        "linux": linux,
        "old_payload_sidecar": old_payload_sidecar,
        "output": output,
    }


def test_materializes_truthful_unsigned_windows_only_shelf(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    result = stage.materialize(values["args"])
    output = values["output"]
    manifest = json.loads((output / stage.CANONICAL_MANIFEST).read_text())
    releases = json.loads((output / stage.COMPATIBILITY_MANIFEST).read_text())
    incumbent_manifest = json.loads(
        (values["incumbent"] / stage.CANONICAL_MANIFEST).read_text()
    )
    incumbent_releases = json.loads(
        (values["incumbent"] / stage.COMPATIBILITY_MANIFEST).read_text()
    )
    windows = next(row for row in manifest["artifacts"] if row["platform"] == "windows")
    linux = next(row for row in manifest["artifacts"] if row["platform"] == "linux")

    assert result["platformScope"] == "windows_only"
    assert result["crossRunBitReproducible"] is False
    assert result["signature"] == {
        "policy": "preview_policy",
        "required": False,
        "status": "unsigned",
    }
    assert manifest["channel"] == "preview"
    assert manifest["version"] == VERSION
    assert manifest["platformScope"] == "windows_only"
    assert manifest["crossRunBitReproducible"] is False
    assert manifest["publicationAuthorized"] is False
    assert manifest["uploadAuthorized"] is False
    assert manifest["deployAuthorized"] is False
    assert all(
        key not in manifest
        for key in stage.REGISTRY_PROJECTION_IDENTITY_KEYS
    )
    assert all(
        key not in releases
        for key in stage.REGISTRY_PROJECTION_IDENTITY_KEYS
    )
    assert all(
        key not in manifest
        for key in stage.OPTIONAL_AUTHORITY_POSTURE_FIELDS
    )
    assert all(
        key not in releases
        for key in stage.OPTIONAL_AUTHORITY_POSTURE_FIELDS
    )
    assert manifest["signature"] == result["signature"]
    assert windows["signature"] == result["signature"]
    assert windows["sha256"] == digest(values["fresh_win"])
    assert windows["payloadSha256"] == digest(values["fresh_payload"])
    payload_sidecar_path = output / "files" / stage.PAYLOAD_SIDECAR_NAME
    assert json.loads(payload_sidecar_path.read_text()) == stage.payload_sidecar(
        VERSION,
        digest(values["fresh_payload"]),
        values["fresh_payload"].stat().st_size,
        stage.DOWNLOAD_ROOT,
    )
    assert result["payloadSidecarSha256"] == digest(payload_sidecar_path)
    assert stat.S_IMODE(payload_sidecar_path.stat().st_mode) == 0o644
    assert linux["sha256"] == digest(values["linux"])
    assert (output / "files" / values["linux"].name).read_bytes() == b"incumbent-linux"
    assert (output / "files" / values["blazor_installer"].name).read_bytes() == (
        b"incumbent-blazor-installer"
    )
    assert (output / "files" / values["blazor_archive"].name).read_bytes() == (
        b"incumbent-blazor-archive"
    )
    assert stat.S_IMODE((output / "files" / values["linux"].name).stat().st_mode) == 0o640
    assert (output / values["ancillary"].name).read_bytes() == b"retain-exactly"
    assert stat.S_IMODE((output / values["ancillary"].name).stat().st_mode) == 0o600
    assert stat.S_IMODE(
        (output / values["ancillary_directory"].name).stat().st_mode
    ) == 0o710
    assert manifest["registryBoundaryCoverage"]["persistence"]["artifactCount"] == 4
    assert manifest["registryBoundaryCoverage"]["compatibility"] == {
        "compatibleArtifactCount": 4,
        "unknownArtifactCount": 0,
    }
    assert manifest["desktopTupleCoverage"]["artifactCount"] == 4
    assert manifest["desktopTupleCoverage"]["installerTupleCount"] == 3
    assert manifest["desktopTupleCoverage"]["requiredDesktopHeads"] == [
        "avalonia",
    ]
    assert releases["desktopTupleCoverage"]["requiredDesktopHeads"] == [
        "avalonia",
    ]
    assert [
        row for row in manifest["artifacts"] if row.get("platform") != "windows"
    ] == [
        row
        for row in incumbent_manifest["artifacts"]
        if row.get("platform") != "windows"
    ]
    assert [
        row
        for row in releases["downloads"]
        if row.get("platformId") != "windows"
    ] == [
        row
        for row in incumbent_releases["downloads"]
        if row.get("platformId") != "windows"
    ]
    windows_download = next(
        row for row in releases["downloads"] if row.get("platformId") == "windows"
    )
    assert windows_download["signature"] == result["signature"]
    assert windows_download["platformScope"] == "windows_only"
    assert windows_download["crossRunBitReproducible"] is False
    assert releases["publicationAuthorized"] is False
    assert releases["uploadAuthorized"] is False
    assert releases["deployAuthorized"] is False


@pytest.mark.parametrize("field", stage.OPTIONAL_AUTHORITY_POSTURE_FIELDS)
@pytest.mark.parametrize(
    "value",
    [
        False,
        True,
        {"unexpected": "downstream-validation"},
    ],
)
def test_apply_release_identity_preserves_non_null_optional_authority_postures(
    field: str,
    value: object,
) -> None:
    result = stage.apply_release_identity(
        {field: value},
        VERSION,
        PUBLISHED_AT,
    )

    assert result[field] == value
    assert type(result[field]) is type(value)


def test_manifest_bytes_are_deterministic_for_identical_inputs(tmp_path: Path) -> None:
    first = fixture(tmp_path / "one")
    second = fixture(tmp_path / "two")
    stage.materialize(first["args"])
    stage.materialize(second["args"])
    for name in (stage.CANONICAL_MANIFEST, stage.COMPATIBILITY_MANIFEST):
        assert (first["output"] / name).read_bytes() == (second["output"] / name).read_bytes()


def test_authenticode_certificate_table_is_rejected(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    values["fresh_win"].write_bytes(
        unsigned_pe(certificate_offset=400, certificate_size=32)
    )
    with pytest.raises(stage.StageError, match="Authenticode certificate table"):
        stage.materialize(values["args"])


def test_missing_incumbent_managed_bytes_fail_closed(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    values["linux"].unlink()
    with pytest.raises(stage.StageError, match="missing bytes"):
        stage.materialize(values["args"])


def test_incumbent_windows_tuple_outside_avalonia_fails_closed(
    tmp_path: Path,
) -> None:
    values = fixture(tmp_path)
    manifest_path = values["incumbent"] / stage.CANONICAL_MANIFEST
    manifest = json.loads(manifest_path.read_text())
    windows = next(
        row for row in manifest["artifacts"] if row["platform"] == "windows"
    )
    windows["head"] = "classic"
    write_json(manifest_path, manifest)
    with pytest.raises(stage.StageError, match="outside avalonia/win-x64"):
        stage.materialize(values["args"])


def test_incumbent_links_fail_closed(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    os.symlink("operator-note.txt", values["incumbent"] / "linked-note.txt")
    with pytest.raises(stage.StageError, match="symbolic link"):
        stage.materialize(values["args"])


def test_existing_output_is_never_overwritten(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    values["output"].mkdir()
    with pytest.raises(stage.StageError, match="output must be absent"):
        stage.materialize(values["args"])


def test_noncanonical_download_root_is_rejected(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    values["args"].download_root = "https://chummer.run/downloads/files/"
    with pytest.raises(stage.StageError, match="must be exactly"):
        stage.materialize(values["args"])


def test_racing_destination_is_not_replaced(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    values = fixture(tmp_path)
    real_rename = stage.atomic_rename_noreplace

    def race(source: Path, target: Path) -> None:
        target.mkdir()
        real_rename(source, target)

    monkeypatch.setattr(stage, "atomic_rename_noreplace", race)
    with pytest.raises(stage.StageError, match="destination"):
        stage.materialize(values["args"])
    assert values["output"].is_dir()
    assert not any(values["output"].iterdir())


def test_atomic_commit_rejects_symlinked_parent_component(tmp_path: Path) -> None:
    physical = tmp_path / "physical"
    physical.mkdir()
    (physical / "source").mkdir()
    linked = tmp_path / "linked"
    linked.symlink_to(physical, target_is_directory=True)
    with pytest.raises(stage.StageError, match="physical publication parent"):
        stage.atomic_rename_noreplace(linked / "source", linked / "target")
    assert (physical / "source").is_dir()
    assert not (physical / "target").exists()


def test_atomic_commit_uses_one_held_parent_dirfd(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    parent = tmp_path / "parent"
    parent.mkdir()
    source = parent / "source"
    target = parent / "target"
    source.mkdir()
    calls: list[tuple[object, ...]] = []

    class RenameAt2:
        argtypes: object = None
        restype: object = None

        def __call__(self, *args: object) -> int:
            calls.append(args)
            source_fd, source_name, target_fd, target_name, flags = args
            assert isinstance(source_fd, int) and isinstance(target_fd, int)
            assert isinstance(source_name, bytes) and isinstance(target_name, bytes)
            assert flags == 1
            os.rename(
                os.fsdecode(source_name),
                os.fsdecode(target_name),
                src_dir_fd=source_fd,
                dst_dir_fd=target_fd,
            )
            return 0

    class Libc:
        renameat2 = RenameAt2()

    monkeypatch.setattr(stage.ctypes, "CDLL", lambda *_args, **_kwargs: Libc())
    stage.atomic_rename_noreplace(source, target)
    assert target.is_dir() and not source.exists()
    assert len(calls) == 1
    assert calls[0][0] == calls[0][2]
    assert calls[0][1:4:2] == (b"source", b"target")


def test_stage_wrapper_is_explicitly_nonpublishing_and_unsigned() -> None:
    wrapper = (
        ROOT / "scripts" / "build-unsigned-windows-preview-nightly-stage.sh"
    ).read_text(encoding="utf-8")
    installer_builder = (
        ROOT / "scripts" / "build-desktop-installer.sh"
    ).read_text(encoding="utf-8")
    assert 'TRUSTED_BASH="/bin/bash"' in wrapper
    assert 'TRUSTED_BASH_PATH="/bin/bash"' in installer_builder
    assert '"$TRUSTED_BASH" "$SCRIPT_DIR/build-desktop-installer.sh"' in wrapper
    assert "CHUMMER_WINDOWS_SIGNING_REQUIRED=0" in wrapper
    assert "CHUMMER_WINDOWS_PUBLICATION_SCOPE_REQUIRED=0" in wrapper
    assert "CHUMMER_WINDOWS_BUILD_PROVENANCE_REQUIRED=0" in wrapper
    assert "CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE=0" in wrapper
    assert "--windows-release-channel preview" in wrapper
    assert "preview_nightly_unsigned_composition.py" in wrapper
    assert '"$TRUSTED_ENV" -i' in wrapper
    assert 'PATH="$TRUSTED_PATH"' in wrapper
    assert 'SOURCE_DATE_EPOCH="$CANONICAL_SOURCE_DATE_EPOCH"' in wrapper
    assert 'CHUMMER_RELEASE_SAMPLE_SOURCE="$SAMPLE_SOURCE"' in wrapper
    assert 'CHUMMER_WINDOWS_SECONDARY_HEAD_KEY=' in wrapper
    assert 'CHUMMER_WINDOWS_SECONDARY_HEAD_PUBLISH_DIR=' in wrapper
    assert 'CHUMMER_WINDOWS_SECONDARY_HEAD_LAUNCH_TARGET=' in wrapper
    assert 'CHUMMER_PUBLIC_DOWNLOADS_PREFIX="$PUBLIC_DOWNLOADS_PREFIX"' in wrapper
    assert 'CHUMMER_WINDOWS_BOOTSTRAP_PAYLOAD_URL="$BOOTSTRAP_PAYLOAD_URL"' in wrapper
    assert 'CHUMMER_WINDOWS_7ZIP_EXTRA_URL="$SEVENZIP_EXTRA_URL"' in wrapper
    assert 'CHUMMER_WINDOWS_7ZIP_EXTRA_SHA256="$SEVENZIP_EXTRA_SHA256"' in wrapper
    assert 'CHUMMER_WINDOWS_CURL_URL="$CURL_WINDOWS_URL"' in wrapper
    assert 'CHUMMER_WINDOWS_CURL_SHA256="$CURL_WINDOWS_SHA256"' in wrapper
    for forbidden in (
        "sign-windows-artifacts.ps1",
        "windows-native-evidence-capture.yml",
        "CHUMMER_FORCE_NIGHTLY_PUBLISH",
        "publish-download-bundle.sh",
        "upload-ticket",
    ):
        assert forbidden not in wrapper


def test_stage_wrapper_rejects_ambient_git_authority_poison() -> None:
    environment = os.environ.copy()
    environment["GIT_DIR"] = "/poison/git"
    environment["GIT_WORK_TREE"] = "/poison/tree"
    completed = subprocess.run(
        [
            "bash",
            str(ROOT / "scripts" / "build-unsigned-windows-preview-nightly-stage.sh"),
            "verify",
        ],
        cwd=ROOT,
        env=environment,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    assert completed.returncode == 2
    assert "ambient GIT_* variables are forbidden" in completed.stdout


@pytest.mark.parametrize(
    "variable_name",
    [
        "SOURCE_DATE_EPOCH",
        "CHUMMER_UI_REPO_ROOT_ALIAS",
        "CHUMMER_PYTHON_BIN",
        "CHUMMER_RELEASE_CHANNEL",
        "CHUMMER_DESKTOP_RELEASE_CHANNEL",
        "CHUMMER_ALLOW_LOCAL_RELEASE_VERSION",
        "CHUMMER_RELEASE_INCLUDE_PDBS",
        "CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE",
        "CHUMMER_WINDOWS_SIGNING_REQUIRED",
        "CHUMMER_WINDOWS_SIGN_PFX_BASE64",
        "CHUMMER_WINDOWS_SIGN_PFX_PATH",
        "CHUMMER_WINDOWS_SIGNING_RECEIPT_PATH",
        "CHUMMER_WINDOWS_PUBLICATION_SCOPE_REQUIRED",
        "CHUMMER_WINDOWS_BUILD_PROVENANCE_REQUIRED",
        "CHUMMER_RELEASE_MANIFEST_STAGE_ONLY",
        "CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS",
        "CHUMMER_WINDOWS_SECONDARY_HEAD_KEY",
        "CHUMMER_WINDOWS_SECONDARY_HEAD_PUBLISH_DIR",
        "CHUMMER_WINDOWS_SECONDARY_HEAD_LAUNCH_TARGET",
        "CHUMMER_WINDOWS_SECONDARY_HEAD_RELATIVE_ROOT",
        "CHUMMER_RELEASE_SAMPLE_SOURCE",
        "CHUMMER_LEGACY_FIXTURE_ROOT",
        "CHUMMER_WINDOWS_INSTALLER_MODE",
        "CHUMMER_PUBLIC_DOWNLOADS_PREFIX",
        "CHUMMER_WINDOWS_BOOTSTRAP_PAYLOAD_URL",
        "CHUMMER_WINDOWS_BOOTSTRAP_ACQUISITION_MODE",
        "CHUMMER_WINDOWS_NATIVE_BOOTSTRAP_TOOLCHAIN_LOCK",
        "CHUMMER_WINDOWS_NATIVE_BOOTSTRAP_TOOLCHAIN_CACHE_DIR",
        "CHUMMER_WINDOWS_7ZIP_EXTRA_URL",
        "CHUMMER_WINDOWS_7ZIP_EXTRA_SHA256",
        "CHUMMER_WINDOWS_CURL_URL",
        "CHUMMER_WINDOWS_CURL_SHA256",
        "CHUMMER_WORKSPACE_ROOT",
        "CHUMMER_WINDOWS_BUILD_PROVENANCE_GENERATOR",
        "CHUMMER_WINDOWS_BUILD_PROVENANCE_SUPPORT",
        "CHUMMER_WINDOWS_SOURCE_CORE_ROOT",
        "CHUMMER_WINDOWS_SOURCE_RUN_SERVICES_ROOT",
        "CHUMMER_WINDOWS_SOURCE_UI_KIT_ROOT",
        "CHUMMER_WINDOWS_SOURCE_REGISTRY_ROOT",
        "CHUMMER_WINDOWS_SOURCE_MEDIA_ROOT",
        "CHUMMER_WINDOWS_SOURCE_LEGACY_ROOT",
    ],
)
def test_stage_wrapper_rejects_ambient_content_authority_poison(
    variable_name: str,
) -> None:
    environment = os.environ.copy()
    for inherited_name in tuple(environment):
        if inherited_name.startswith("GIT_"):
            environment.pop(inherited_name)
    environment[variable_name] = "/poison/content-authority"
    completed = subprocess.run(
        [
            "bash",
            str(ROOT / "scripts" / "build-unsigned-windows-preview-nightly-stage.sh"),
            "verify",
        ],
        cwd=ROOT,
        env=environment,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    assert completed.returncode == 2
    assert (
        "ambient content-affecting builder variable is forbidden: "
        f"{variable_name}"
    ) in completed.stdout
