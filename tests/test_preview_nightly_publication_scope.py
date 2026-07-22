from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import shutil
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "preview_nightly_publication_scope.py"
SPEC = importlib.util.spec_from_file_location("preview_nightly_publication_scope", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
scope = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = scope
SPEC.loader.exec_module(scope)

COMMIT = "1" * 40
GENERATED_AT = "2026-07-21T12:00:00Z"
DOWNLOAD_ROOT = "https://chummer.run/downloads/files"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_bytes(root: Path, name: str, value: bytes) -> tuple[str, int]:
    path = root / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(value)
    return digest(path), path.stat().st_size


def artifact(
    *,
    artifact_id: str,
    head: str,
    platform: str,
    rid: str,
    file_name: str,
    sha256: str,
    size: int,
    payload: tuple[str, str, int] | None = None,
) -> dict[str, object]:
    platform_names = {
        "linux": "Linux",
        "macos": "macOS",
        "windows": "Windows",
    }
    arch = rid.rsplit("-", 1)[-1]
    arch_label = {"arm64": "ARM64", "x64": "X64"}[arch]
    row: dict[str, object] = {
        "artifactId": artifact_id,
        "id": artifact_id,
        "fileName": file_name,
        "head": head,
        "kind": "installer",
        "platform": platform,
        "platformLabel": (
            f"Avalonia Desktop {platform_names[platform]} {arch_label} Installer"
        ),
        "rid": rid,
        "arch": arch,
        "downloadUrl": f"{DOWNLOAD_ROOT}/{file_name}",
        "sha256": sha256,
        "sizeBytes": size,
        "compatibilityState": "compatible",
        "compatibilityReason": None,
        "installAccessClass": "open_public",
        "installerMode": "bootstrap" if payload is not None else "offline",
        "payloadFileName": None,
        "payloadDownloadUrl": None,
        "payloadSha256": None,
        "payloadSizeBytes": None,
    }
    if payload is not None:
        row.update(
            {
                "payloadAcquisitionMode": "download",
                "payloadFileName": payload[0],
                "payloadDownloadUrl": f"{DOWNLOAD_ROOT}/{payload[0]}",
                "payloadSha256": payload[1],
                "payloadSizeBytes": payload[2],
            }
        )
    return row


def manifest(version: str, rows: list[dict[str, object]]) -> dict[str, object]:
    artifacts = []
    for source in rows:
        row = dict(source)
        row.update(
            {
                "channelId": "preview",
                "channel": "preview",
                "version": version,
                "releaseVersion": version,
                "generated_at": GENERATED_AT,
                "generatedAt": GENERATED_AT,
            }
        )
        artifacts.append(row)
    return {
        "channel": "preview",
        "channelId": "preview",
        "contract_name": "Chummer.Hub.Registry.Contracts",
        "contractName": "Chummer.Hub.Registry.Contracts",
        "generated_at": GENERATED_AT,
        "generatedAt": GENERATED_AT,
        "publishedAt": GENERATED_AT,
        "releaseVersion": version,
        "schemaVersion": 1,
        "version": version,
        "artifacts": artifacts,
    }


def compatibility(version: str, rows: list[dict[str, object]]) -> dict[str, object]:
    downloads = []
    for source in rows:
        file_name = source["fileName"]
        row = {
            "id": source["artifactId"],
            "platform": source["platformLabel"],
            "url": source["downloadUrl"],
            "sha256": source["sha256"],
            "sizeBytes": source["sizeBytes"],
            "format": scope._registry_format(file_name),
            "flavor": source["kind"],
            "kind": source["kind"],
            "head": source["head"],
            "platformId": source["platform"],
            "arch": source["arch"],
            "rid": source["rid"],
            "fileName": file_name,
            "channelId": "preview",
            "channel": "preview",
            "version": version,
            "releaseVersion": version,
            "compatibilityState": source["compatibilityState"],
            "compatibilityReason": source["compatibilityReason"],
            "installerMode": source["installerMode"],
            "payloadFileName": source["payloadFileName"],
            "payloadDownloadUrl": source["payloadDownloadUrl"],
            "payloadSha256": source["payloadSha256"],
            "payloadSizeBytes": source["payloadSizeBytes"],
            "installAccessClass": source["installAccessClass"],
            "artifactId": source["artifactId"],
        }
        if "payloadAcquisitionMode" in source:
            row["payloadAcquisitionMode"] = source["payloadAcquisitionMode"]
        downloads.append(row)
    return {
        "channel": "preview",
        "channelId": "preview",
        "contract_name": "Chummer.Hub.Registry.Contracts",
        "contractName": "Chummer.Hub.Registry.Contracts",
        "generated_at": GENERATED_AT,
        "generatedAt": GENERATED_AT,
        "publishedAt": GENERATED_AT,
        "releaseVersion": version,
        "schemaVersion": 1,
        "version": version,
        "downloads": downloads,
    }


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def fixture(
    tmp_path: Path,
    *,
    incumbent_platforms: frozenset[str] = frozenset({"macos"}),
) -> dict[str, object]:
    version = "run-20260721-134142"
    evidence_root = tmp_path / "evidence"
    incumbent_shelf = tmp_path / "incumbent-shelf"
    build_files = evidence_root / "build-files"
    incumbent_files = incumbent_shelf / "files"
    evidence_root.mkdir()
    build_files.mkdir()
    incumbent_shelf.mkdir()
    incumbent_files.mkdir()

    win_sha, win_size = write_bytes(
        build_files, "chummer-avalonia-win-x64-installer.exe", b"signed-win-v2"
    )
    payload_sha, payload_size = write_bytes(
        build_files, "chummer-avalonia-win-x64-payload.zip", b"payload-v2"
    )
    linux_sha, linux_size = write_bytes(
        build_files, "chummer-avalonia-linux-x64-installer.deb", b"fresh-linux-evidence"
    )
    build_rows = [
        artifact(
            artifact_id="avalonia-win-x64-installer",
            head="avalonia",
            platform="windows",
            rid="win-x64",
            file_name="chummer-avalonia-win-x64-installer.exe",
            sha256=win_sha,
            size=win_size,
            payload=("chummer-avalonia-win-x64-payload.zip", payload_sha, payload_size),
        ),
        artifact(
            artifact_id="avalonia-linux-x64-installer",
            head="avalonia",
            platform="linux",
            rid="linux-x64",
            file_name="chummer-avalonia-linux-x64-installer.deb",
            sha256=linux_sha,
            size=linux_size,
        ),
    ]

    if not incumbent_platforms or not incumbent_platforms.issubset(
        {"linux", "windows", "macos"}
    ):
        raise AssertionError("fixture incumbent platforms are invalid")
    incumbent_rows: list[dict[str, object]] = []
    if "windows" in incumbent_platforms:
        old_win_sha, old_win_size = write_bytes(
            incumbent_files, "chummer-avalonia-win-x64-installer.exe", b"old-win"
        )
        old_payload_sha, old_payload_size = write_bytes(
            incumbent_files, "chummer-avalonia-win-x64-payload.zip", b"old-payload"
        )
        incumbent_rows.append(
            artifact(
                artifact_id="avalonia-win-x64-installer",
                head="avalonia",
                platform="windows",
                rid="win-x64",
                file_name="chummer-avalonia-win-x64-installer.exe",
                sha256=old_win_sha,
                size=old_win_size,
                payload=(
                    "chummer-avalonia-win-x64-payload.zip",
                    old_payload_sha,
                    old_payload_size,
                ),
            )
        )
    if "linux" in incumbent_platforms:
        old_linux_sha, old_linux_size = write_bytes(
            incumbent_files, "chummer-avalonia-linux-x64-installer.deb", b"old-linux"
        )
        incumbent_rows.append(
            artifact(
                artifact_id="avalonia-linux-x64-installer",
                head="avalonia",
                platform="linux",
                rid="linux-x64",
                file_name="chummer-avalonia-linux-x64-installer.deb",
                sha256=old_linux_sha,
                size=old_linux_size,
            )
        )
    mac_sha = ""
    if "macos" in incumbent_platforms:
        mac_sha, mac_size = write_bytes(
            incumbent_files,
            "chummer-avalonia-osx-arm64-installer.dmg",
            b"approved-macos",
        )
        incumbent_rows.append(
            artifact(
                artifact_id="avalonia-osx-arm64-installer",
                head="avalonia",
                platform="macos",
                rid="osx-arm64",
                file_name="chummer-avalonia-osx-arm64-installer.dmg",
                sha256=mac_sha,
                size=mac_size,
            )
        )
    paths = {
        "build_manifest": evidence_root / "build-manifest.json",
        "build_releases": evidence_root / "build-releases.json",
        "incumbent_manifest": incumbent_shelf / scope.CANONICAL_MANIFEST_NAME,
        "incumbent_releases": incumbent_shelf / scope.COMPATIBILITY_MANIFEST_NAME,
        "signing_receipt": evidence_root / "signing-input.json",
        "publication_dir": evidence_root / "publication",
        "output": evidence_root / scope.PROPOSAL_FILE_NAME,
    }
    write_json(paths["build_manifest"], manifest(version, build_rows))
    write_json(paths["build_releases"], compatibility(version, build_rows))
    write_json(paths["incumbent_manifest"], manifest("incumbent-v1", incumbent_rows))
    write_json(paths["incumbent_releases"], compatibility("incumbent-v1", incumbent_rows))
    (incumbent_shelf / "aur-packages.json").write_bytes(b"incumbent-aur-index")
    (incumbent_shelf / "aur-packages.json").chmod(0o640)
    (incumbent_files / "chummer6-bin-aur-source.tar.gz").write_bytes(
        b"incumbent-aur-source"
    )
    (incumbent_files / "chummer6-bin.PKGBUILD").write_bytes(b"incumbent-pkgbuild")
    (incumbent_files / "chummer6-bin.PKGBUILD").chmod(0o755)
    (incumbent_files / "chummer6-bin.SRCINFO").write_bytes(b"incumbent-srcinfo")
    (incumbent_shelf / "operator-note.txt").write_bytes(b"preserve-me")
    (incumbent_shelf / "operator-note.txt").chmod(0o640)
    write_json(
        paths["signing_receipt"],
        {
            "app": "avalonia",
            "artifacts": [
                {
                    "fileName": "chummer-avalonia-win-x64-installer.exe",
                    "kind": "installer",
                    "sha256": win_sha,
                    "signingStatus": "pass",
                }
            ],
            "candidateBindings": [
                {
                    "artifactRole": "installer",
                    "authenticodeStatus": "pass",
                    "fileName": "chummer-avalonia-win-x64-installer.exe",
                    "sha256": win_sha,
                    "sizeBytes": win_size,
                },
                {
                    "artifactRole": "payload",
                    "authenticodeStatus": "not_applicable_payload",
                    "fileName": "chummer-avalonia-win-x64-payload.zip",
                    "sha256": payload_sha,
                    "sizeBytes": payload_size,
                },
            ],
            "contractName": "chummer6-ui.desktop_artifact_signing",
            "contractVersion": 2,
            "platform": "windows",
            "releaseChannel": "preview",
            "releaseVersion": version,
            "rid": "win-x64",
            "signingStatus": "pass",
        },
    )
    args = argparse.Namespace(
        **paths,
        build_files_dir=build_files,
        incumbent_files_dir=incumbent_files,
        incumbent_shelf_dir=incumbent_shelf,
        incumbent_snapshot_dir=evidence_root / "retained-full-source",
        consumer_commit=COMMIT,
        build_manifest_receipt_path="BUILD_EVIDENCE_RELEASE_CHANNEL.generated.json",
        incumbent_manifest_receipt_path="retained-source/RELEASE_CHANNEL.generated.json",
    )
    return {
        "args": args,
        "evidence_root": evidence_root,
        "incumbent_shelf": incumbent_shelf,
        "paths": paths,
        "version": version,
        "win_sha": win_sha,
        "payload_sha": payload_sha,
        "linux_sha": linux_sha,
        "mac_sha": mac_sha,
        "incumbent_platforms": incumbent_platforms,
    }


def prepare(tmp_path: Path) -> tuple[dict[str, object], dict[str, object]]:
    values = fixture(tmp_path)
    payload = scope.prepare_scope(values["args"])
    return values, payload


def registry_prepare_binding() -> dict[str, object]:
    output_inventory = [
        {
            "mode": "0644",
            "path": name,
            "sha256": str(index) * 64,
            "sizeBytes": index,
        }
        for index, name in enumerate(
            sorted(scope.REGISTRY_PREPARE_OUTPUT_NAMES),
            start=1,
        )
    ]
    candidate = next(
        row
        for row in output_inventory
        if row["path"] == "PREVIEW_PUBLICATION_DELTA_CANDIDATE.json"
    )
    return {
        "candidateReceiptSha256": candidate["sha256"],
        "composition": {
            "mode": "0644",
            "path": "registry-prepare/composition.json",
            "sha256": "4" * 64,
            "sizeBytes": 4,
        },
        "contractName": scope.REGISTRY_PREPARE_CONTRACT_NAME,
        "contractVersion": scope.REGISTRY_PREPARE_CONTRACT_VERSION,
        "deployAuthority": False,
        "finalizeAvailable": True,
        "finalizeReceipt": None,
        "inputRoots": {
            name: {
                "fileCount": 2,
                "inventorySha256": digest_value * 64,
                "path": f"registry-prepare/inputs/{name}",
            }
            for name, digest_value in (
                ("delta", "5"),
                ("evidence", "6"),
                ("incumbent", "7"),
            )
        },
        "outputInventory": output_inventory,
        "outputInventorySha256": scope.registry_document_sha256(output_inventory),
        "projectionInputs": json.loads(json.dumps(scope.REGISTRY_PROJECTION_INPUTS)),
        "publicationEligible": False,
        "registryCommit": scope.REGISTRY_AUTHORITY_COMMIT,
        "releaseUploadAuthority": False,
        "routeAuthority": False,
        "status": "review_required",
        "wholeDirectoryVerified": True,
    }


def test_merged_registry_finalize_source_pins_and_prepare_posture_are_frozen() -> None:
    assert scope.REGISTRY_AUTHORITY_COMMIT == (
        "01c08982348432cab71ae461e231ce9a42084911"
    )
    assert scope.REGISTRY_PROJECTION_INPUTS == {
        "materializer": {
            "path": "scripts/materialize_preview_publication_delta.py",
            "sha256": (
                "74c88878f2219d35bcae258a86a976162982cc4200779ee0312ef1d09202bb70"
            ),
            "sizeBytes": 202660,
        },
        "releaseChannelMaterializer": {
            "path": "scripts/materialize_public_release_channel.py",
            "sha256": (
                "333cb21427e495314aab5f870af1d7130c588f444d023e9b89ce69f3e9d76027"
            ),
            "sizeBytes": 241522,
        },
        "schema": {
            "path": "contracts/preview-publication-delta-v1.schema.json",
            "sha256": (
                "27af4db39bc9435864d6e038c36c225302c1d4e0d3792e59d554f36d529e8f79"
            ),
            "sizeBytes": 27754,
        },
        "verifier": {
            "path": "scripts/verify_public_release_channel.py",
            "sha256": (
                "3488aa9688c066247a54df513a8e314963bc4b14f3c495aa30423205945c29f4"
            ),
            "sizeBytes": 383489,
        },
    }
    binding = registry_prepare_binding()
    scope.validate_registry_prepare_binding(binding)
    for mutation in (
        {"finalizeAvailable": False},
        {"finalizeReceipt": {"path": "not-earned.json"}},
    ):
        forged = {**binding, **mutation}
        with pytest.raises(scope.ScopeError, match="fail-closed authority"):
            scope.validate_registry_prepare_binding(forged)


def test_composes_complete_shelf_but_publishes_only_windows_delta(tmp_path: Path) -> None:
    values, payload = prepare(tmp_path)
    assert {
        row["platform"] for row in payload["buildEvidenceTuples"]
    } == {"windows", "linux"}
    assert {row["platform"] for row in payload["publicationDeltaTuples"]} == {"windows"}
    assert {row["platform"] for row in payload["nonPublishedEvidenceTuples"]} == {"linux"}
    assert payload["authenticodeRequired"] is True
    assert payload["uploadAuthorized"] is False
    assert payload["publicationEligible"] is False
    assert payload["registryFinalizeEligible"] is False
    assert payload["macosSoak"]["reason"] == "retained_byte_identical"
    public = values["paths"]["publication_dir"]
    public_manifest = json.loads((public / scope.CANONICAL_MANIFEST_NAME).read_text())
    platforms = {row["platform"] for row in public_manifest["artifacts"]}
    assert platforms == {"windows", "macos"}
    assert public_manifest["desktopTupleCoverage"]["requiredDesktopPlatforms"] == [
        "macos",
        "windows",
    ]
    assert not (
        public / "files/chummer-avalonia-linux-x64-installer.deb"
    ).exists()
    assert payload["incumbentSnapshot"]["platforms"] == ["macos"]


def test_optional_incumbent_linux_is_retained_but_fresh_linux_is_never_published(
    tmp_path: Path,
) -> None:
    values = fixture(
        tmp_path, incumbent_platforms=frozenset({"linux", "macos"})
    )
    payload = scope.prepare_scope(values["args"])
    public = values["paths"]["publication_dir"]
    linux = next(
        row for row in payload["postPublicationShelfTuples"] if row["platform"] == "linux"
    )
    assert linux["sha256"] != values["linux_sha"]
    assert (public / linux["path"]).read_bytes() == b"old-linux"
    assert {row["platform"] for row in payload["nonPublishedEvidenceTuples"]} == {
        "linux"
    }


def test_macos_soak_is_nonblocking_when_no_incumbent_macos_tuple(
    tmp_path: Path,
) -> None:
    values = fixture(tmp_path, incumbent_platforms=frozenset({"linux"}))
    payload = scope.prepare_scope(values["args"])

    assert payload["macosSoak"] == {
        "byteIdentical": False,
        "incumbentTupleSetSha256": scope.canonical_sha256([]),
        "postPublicationTupleSetSha256": scope.canonical_sha256([]),
        "reason": "not_applicable_no_incumbent_tuple",
        "required": False,
    }
    assert all(
        row["platform"] != "macos" for row in payload["postPublicationShelfTuples"]
    )
    scope.validate_proposal(payload)


def test_optional_incumbent_windows_pair_is_replaced_exactly(tmp_path: Path) -> None:
    values = fixture(
        tmp_path, incumbent_platforms=frozenset({"windows", "macos"})
    )
    payload = scope.prepare_scope(values["args"])
    assert payload["incumbentSnapshot"]["platforms"] == ["macos", "windows"]
    final_windows = {
        row["sha256"]
        for row in payload["postPublicationShelfTuples"]
        if row["platform"] == "windows"
    }
    assert final_windows == {values["win_sha"], values["payload_sha"]}
    assert all(row["platform"] != "windows" for row in payload["retainedTuples"])


@pytest.mark.parametrize("mutation", ["unsigned", "wrong_installer", "missing_payload"])
def test_signing_and_complete_delta_fail_closed(tmp_path: Path, mutation: str) -> None:
    values = fixture(tmp_path)
    paths = values["paths"]
    if mutation == "unsigned":
        receipt = json.loads(paths["signing_receipt"].read_text())
        receipt["signingStatus"] = "skipped_preview"
        write_json(paths["signing_receipt"], receipt)
    elif mutation == "wrong_installer":
        receipt = json.loads(paths["signing_receipt"].read_text())
        receipt["candidateBindings"][0]["sha256"] = "f" * 64
        write_json(paths["signing_receipt"], receipt)
    else:
        manifest_payload = json.loads(paths["build_manifest"].read_text())
        row = next(row for row in manifest_payload["artifacts"] if row["platform"] == "windows")
        for key in ("payloadFileName", "payloadSha256", "payloadSizeBytes"):
            row.pop(key)
        write_json(paths["build_manifest"], manifest_payload)
    with pytest.raises(scope.ScopeError):
        scope.prepare_scope(values["args"])


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("sha256", "f" * 64),
        ("sizeBytes", 999999),
        ("artifactId", "another-artifact"),
        ("payloadSha256", "e" * 64),
        (
            "downloadUrl",
            "https://example.invalid/other/chummer-avalonia-win-x64-installer.exe",
        ),
    ],
)
def test_compatibility_row_must_exactly_match_canonical_artifact(
    tmp_path: Path, field: str, value: object
) -> None:
    values = fixture(tmp_path)
    path = values["paths"]["build_releases"]
    releases = json.loads(path.read_text(encoding="utf-8"))
    windows = next(
        row for row in releases["downloads"] if row["platformId"] == "windows"
    )
    windows[field] = value
    write_json(path, releases)

    with pytest.raises(scope.ScopeError, match="compatibility"):
        scope.prepare_scope(values["args"])


def test_compatibility_output_is_fresh_current_registry_projection(
    tmp_path: Path,
) -> None:
    values = fixture(tmp_path)
    for key in ("build_releases", "incumbent_releases"):
        path = values["paths"][key]
        releases = json.loads(path.read_text(encoding="utf-8"))
        releases["untrustedTopLevelExtension"] = "must-not-pass-through"
        for row in releases["downloads"]:
            row["untrustedRowExtension"] = "must-not-pass-through"
        write_json(path, releases)

    scope.prepare_scope(values["args"])
    published = json.loads(
        (
            values["paths"]["publication_dir"]
            / scope.COMPATIBILITY_MANIFEST_NAME
        ).read_text(encoding="utf-8")
    )

    assert set(published) == {
        "channel",
        "channelId",
        "contractName",
        "contract_name",
        "desktopTupleCoverage",
        "downloads",
        "generatedAt",
        "generated_at",
        "publicVersion",
        "publishedAt",
        "releaseVersion",
        "version",
    }
    expected_row_keys = {
        "arch",
        "artifactId",
        "channel",
        "channelId",
        "compatibilityReason",
        "compatibilityState",
        "fileName",
        "flavor",
        "format",
        "head",
        "id",
        "installAccessClass",
        "installerMode",
        "kind",
        "payloadDownloadUrl",
        "payloadFileName",
        "payloadSha256",
        "payloadSizeBytes",
        "platform",
        "platformId",
        "releaseVersion",
        "rid",
        "sha256",
        "sizeBytes",
        "url",
        "version",
    }
    for row in published["downloads"]:
        assert set(row) in (
            expected_row_keys,
            expected_row_keys | {"payloadAcquisitionMode"},
        )
        assert row["platform"] != row["platformId"]
        assert row["version"] == values["version"]
        assert row["releaseVersion"] == values["version"]


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("contractName", "Poison.Registry.Contract"),
        ("contract_name", "Poison.Registry.Contract"),
        ("channelId", "staging"),
        ("channel", "staging"),
        ("version", "poison-version"),
        ("releaseVersion", "poison-version"),
        ("generatedAt", "2026-07-21T12:00:01Z"),
        ("generated_at", "2026-07-21T12:00:01Z"),
    ],
)
def test_compatibility_top_level_alias_conflicts_fail_closed(
    tmp_path: Path, field: str, value: object
) -> None:
    values = fixture(tmp_path)
    path = values["paths"]["build_releases"]
    releases = json.loads(path.read_text(encoding="utf-8"))
    releases[field] = value
    write_json(path, releases)

    with pytest.raises(scope.ScopeError):
        scope.prepare_scope(values["args"])


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("artifactId", "poison-artifact"),
        ("id", "poison-artifact"),
        ("fileName", "poison-installer.exe"),
        ("name", "poison-installer.exe"),
        ("url", f"{DOWNLOAD_ROOT}/poison-installer.exe"),
        (
            "downloadUrl",
            "https://example.invalid/files/chummer-avalonia-win-x64-installer.exe",
        ),
        ("head", "poison-head"),
        ("headId", "poison-head"),
        ("platformId", "linux"),
        ("platform", "Legacy Windows Display Value"),
        ("platformLabel", "Conflicting Windows Display Value"),
        ("rid", "win-arm64"),
        ("arch", "arm64"),
        ("kind", "archive"),
        ("flavor", "archive"),
        ("format", "zip"),
        ("channelId", "staging"),
        ("channel", "staging"),
        ("version", "poison-version"),
        ("releaseVersion", "poison-version"),
        ("sha256", "f" * 64),
        ("artifactSha256", "f" * 64),
        ("digest", "sha256:" + "f" * 64),
        ("sizeBytes", 1.0),
        ("artifactSizeBytes", 1.0),
        ("size", 1.0),
        ("payloadFileName", "poison-payload.zip"),
        ("payloadName", "poison-payload.zip"),
        (
            "payloadDownloadUrl",
            f"{DOWNLOAD_ROOT}/poison-payload.zip",
        ),
        (
            "payloadUrl",
            "https://example.invalid/files/chummer-avalonia-win-x64-payload.zip",
        ),
        ("payloadSha256", "f" * 64),
        ("payloadArtifactSha256", "f" * 64),
        ("payloadDigest", "sha256:" + "f" * 64),
        ("payloadSizeBytes", 1.0),
        ("payloadSize", 1.0),
    ],
)
def test_every_compatibility_row_alias_is_strict_and_unambiguous(
    tmp_path: Path, field: str, value: object
) -> None:
    values = fixture(tmp_path)
    path = values["paths"]["build_releases"]
    releases = json.loads(path.read_text(encoding="utf-8"))
    windows = next(
        row for row in releases["downloads"] if row["platformId"] == "windows"
    )
    windows[field] = value
    write_json(path, releases)

    with pytest.raises(scope.ScopeError):
        scope.prepare_scope(values["args"])


@pytest.mark.parametrize("payload", [False, True])
def test_registry_url_basenames_are_bound_to_declared_names(
    tmp_path: Path, payload: bool
) -> None:
    values = fixture(tmp_path)
    canonical_path = values["paths"]["build_manifest"]
    compatibility_path = values["paths"]["build_releases"]
    canonical = json.loads(canonical_path.read_text(encoding="utf-8"))
    releases = json.loads(compatibility_path.read_text(encoding="utf-8"))
    canonical_windows = next(
        row for row in canonical["artifacts"] if row["platform"] == "windows"
    )
    compatibility_windows = next(
        row for row in releases["downloads"] if row["platformId"] == "windows"
    )
    if payload:
        bad_url = f"{DOWNLOAD_ROOT}/wrong-payload.zip"
        canonical_windows["payloadDownloadUrl"] = bad_url
        compatibility_windows["payloadDownloadUrl"] = bad_url
    else:
        bad_url = f"{DOWNLOAD_ROOT}/wrong-installer.exe"
        canonical_windows["downloadUrl"] = bad_url
        compatibility_windows["url"] = bad_url
    write_json(canonical_path, canonical)
    write_json(compatibility_path, releases)

    with pytest.raises(scope.ScopeError, match="basename"):
        scope.prepare_scope(values["args"])


def test_legacy_display_platform_without_machine_and_label_fails_with_migration(
    tmp_path: Path,
) -> None:
    values = fixture(tmp_path)
    canonical_path = values["paths"]["build_manifest"]
    canonical = json.loads(canonical_path.read_text(encoding="utf-8"))
    windows = next(
        row for row in canonical["artifacts"] if row["platform"] == "windows"
    )
    windows.pop("platformLabel")
    write_json(canonical_path, canonical)

    with pytest.raises(scope.ScopeError, match="migrate"):
        scope.prepare_scope(values["args"])


def test_legacy_compatibility_display_platform_without_machine_id_fails_migration(
    tmp_path: Path,
) -> None:
    values = fixture(tmp_path)
    compatibility_path = values["paths"]["build_releases"]
    releases = json.loads(compatibility_path.read_text(encoding="utf-8"))
    windows = next(
        row for row in releases["downloads"] if row["platformId"] == "windows"
    )
    windows.pop("platformId")
    write_json(compatibility_path, releases)

    with pytest.raises(scope.ScopeError, match="migrate older display-platform"):
        scope.prepare_scope(values["args"])


def test_pinned_registry_prepare_is_the_exact_three_file_manifest_authority(
    tmp_path: Path,
) -> None:
    configured = os.environ.get("CHUMMER_UI_TEST_REGISTRY_ROOT")
    if not configured:
        pytest.skip("exact merged Registry FINALIZE authority is not configured")
    registry_root = Path(configured).resolve(strict=True)
    values = fixture(tmp_path)
    values["args"].registry_root = registry_root
    values["args"].registry_prepare_root = (
        values["evidence_root"] / "registry-prepare"
    )
    values["args"].desktop_commit = "2" * 40

    proposal = scope.prepare_scope(values["args"])
    scope.validate_proposal(proposal)
    binding = proposal["registryPrepare"]
    assert binding["registryCommit"] == scope.REGISTRY_AUTHORITY_COMMIT
    assert binding["projectionInputs"] == scope.REGISTRY_PROJECTION_INPUTS
    assert binding["wholeDirectoryVerified"] is True
    assert binding["finalizeAvailable"] is True
    assert binding["finalizeReceipt"] is None
    assert binding["publicationEligible"] is False
    assert binding["releaseUploadAuthority"] is False
    assert binding["deployAuthority"] is False
    assert binding["routeAuthority"] is False
    assert [row["path"] for row in binding["outputInventory"]] == sorted(
        scope.REGISTRY_PREPARE_OUTPUT_NAMES
    )
    assert {row["mode"] for row in binding["outputInventory"]} == {"0644"}
    prepare_output = values["args"].registry_prepare_root / "output"
    publication = values["paths"]["publication_dir"]
    for name in (
        scope.CANONICAL_MANIFEST_NAME,
        scope.COMPATIBILITY_MANIFEST_NAME,
    ):
        assert (publication / name).read_bytes() == (prepare_output / name).read_bytes()
    assert (publication / scope.REGISTRY_INCUMBENT_LINEAGE_PATH).is_file()
    replay = scope.replay_registry_prepare(
        binding,
        values["evidence_root"],
        registry_root,
    )
    assert replay == {
        "contractName": "chummer6-ui.registry-preview-prepare-replay",
        "contractVersion": 1,
        "outputInventorySha256": binding["outputInventorySha256"],
        "registryCommit": scope.REGISTRY_AUTHORITY_COMMIT,
        "registryPrepareSha256": scope.canonical_sha256(binding),
        "status": "reproduced",
        "wholeDirectoryVerified": True,
    }


def test_v2_prepare_requires_explicit_incumbent_and_snapshot_roots(
    tmp_path: Path,
) -> None:
    values = fixture(tmp_path)
    del values["args"].incumbent_shelf_dir
    with pytest.raises(scope.ScopeError, match="explicit incumbent full shelf"):
        scope.prepare_scope(values["args"])

    second_root = tmp_path / "second"
    second_root.mkdir()
    values = fixture(second_root)
    del values["args"].incumbent_snapshot_dir
    with pytest.raises(scope.ScopeError, match="explicit sealed incumbent snapshot"):
        scope.prepare_scope(values["args"])


def test_prepare_rejects_recursive_or_overlapping_snapshot_root(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    values["args"].incumbent_snapshot_dir = values["incumbent_shelf"] / "snapshot"
    with pytest.raises(scope.ScopeError, match="ancestor/descendant"):
        scope.prepare_scope(values["args"])


@pytest.mark.parametrize(
    "mutation",
    ["symlink", "fifo", "hardlink", "case_collision", "windows_invalid"],
)
def test_full_incumbent_source_rejects_nonportable_or_aliased_entries(
    tmp_path: Path, mutation: str
) -> None:
    values = fixture(tmp_path)
    incumbent = values["incumbent_shelf"]
    if mutation == "symlink":
        (incumbent / "bad-link").symlink_to("operator-note.txt")
    elif mutation == "fifo":
        os.mkfifo(incumbent / "bad-fifo")
    elif mutation == "hardlink":
        os.link(incumbent / "operator-note.txt", incumbent / "operator-note-alias.txt")
    elif mutation == "case_collision":
        (incumbent / "Case.txt").write_bytes(b"first")
        (incumbent / "case.TXT").write_bytes(b"second")
    else:
        (incumbent / "bad:name.txt").write_bytes(b"invalid on Windows")
    with pytest.raises(scope.ScopeError):
        scope.prepare_scope(values["args"])


def test_full_shelf_inventory_binds_ancillary_and_managed_permission_modes(
    tmp_path: Path,
) -> None:
    _, payload = prepare(tmp_path)
    inventory = {row["path"]: row for row in payload["fullShelfInventory"]}
    snapshot = {
        row["path"]: row for row in payload["incumbentSnapshot"]["inventory"]
    }
    assert inventory["files/chummer6-bin.PKGBUILD"]["mode"] == 0o755
    assert inventory["operator-note.txt"]["mode"] == 0o640
    assert snapshot["files/chummer6-bin.PKGBUILD"]["mode"] == 0o755
    assert snapshot[scope.CANONICAL_MANIFEST_NAME]["mode"] == inventory[
        scope.CANONICAL_MANIFEST_NAME
    ]["mode"]


def test_old_scope_contract_and_macos_drift_are_rejected(tmp_path: Path) -> None:
    _, payload = prepare(tmp_path)
    old = dict(payload)
    old["contractVersion"] = 1
    with pytest.raises(scope.ScopeError, match="old or unsupported"):
        scope.validate_proposal(old)
    drifted = json.loads(json.dumps(payload))
    mac = next(row for row in drifted["postPublicationShelfTuples"] if row["platform"] == "macos")
    mac["sha256"] = "e" * 64
    with pytest.raises(scope.ScopeError, match="retained union|macOS"):
        scope.validate_proposal(drifted)


def approval_for(
    payload: dict[str, object],
    proposal_path: Path,
    actor: str = "independent-reviewer",
    authenticode_sha256: str = "a" * 64,
) -> dict[str, object]:
    return {
        "approvedAt": "2026-07-21T17:00:00Z",
        "approver": actor,
        "authenticodeVerificationSha256": authenticode_sha256,
        "contractName": scope.APPROVAL_CONTRACT_NAME,
        "contractVersion": scope.CONTRACT_VERSION,
        "fullShelfCompatibilityManifestSha256": payload[
            "fullShelfCompatibilityManifestSha256"
        ],
        "fullShelfInventorySha256": payload["fullShelfInventorySha256"],
        "fullShelfManifestSha256": payload["fullShelfManifestSha256"],
        "incumbentSnapshotSha256": payload["incumbentSnapshotSha256"],
        "publicationDeltaSha256": scope.canonical_sha256(payload["publicationDeltaTuples"]),
        "publicationScopeProposalSha256": digest(proposal_path),
        "registryPrepareSha256": (
            scope.canonical_sha256(payload["registryPrepare"])
            if payload.get("registryPrepare") is not None
            else None
        ),
        "scopeDecisionSha256": payload["scopeDecisionSha256"],
        "signingReceiptSha256": payload["signingReceiptSha256"],
        "status": "approved",
    }


def write_authenticode_evidence(
    evidence_root: Path,
    proposal: dict[str, object],
) -> dict[str, object]:
    installer = next(
        row
        for row in proposal["publicationDeltaTuples"]
        if row["artifactRole"] == "installer"
    )
    timestamp = "2026-01-15T12:00:00.0000000Z"
    chain = {
        "revocationFlag": "entire_chain",
        "revocationMode": "online",
        "status": [],
        "trusted": True,
        "verificationFlags": "no_flag",
        "verificationTimeUtc": timestamp,
    }
    capture_source = {
        "actor": "capture-bot",
        "artifactName": "windows-native-evidence-12345-1",
        "ref": "refs/heads/main",
        "repository": "ArchonMegalon/chummer6-ui",
        "runAttempt": "1",
        "runId": "12345",
        "sha": COMMIT,
        "workflow": ".github/workflows/windows-native-evidence-capture.yml",
    }
    receipt = {
        "artifact": {
            "fileName": installer["fileName"],
            "sha256": installer["sha256"],
            "sizeBytes": installer["sizeBytes"],
        },
        "contractName": scope.AUTHENTICODE_VERIFICATION_CONTRACT_NAME,
        "contractVersion": 1,
        "generatedAt": "2026-07-20T12:00:00.0000000Z",
        "policy": {
            "signerCertificateSha256": "1" * 64,
            "signerSpkiSha256": "2" * 64,
        },
        "signature": {
            "codeSigningEkuOid": "1.3.6.1.5.5.7.3.3",
            "cryptographicVerification": "passed",
            "status": "valid",
            "type": "authenticode",
        },
        "signer": {
            "certificateSha256": "1" * 64,
            "chain": dict(chain),
            "issuer": "CN=Test Root",
            "notAfterUtc": "2030-01-01T00:00:00.0000000Z",
            "notBeforeUtc": "2025-01-01T00:00:00.0000000Z",
            "serialNumber": "01",
            "spkiSha256": "2" * 64,
            "subject": "CN=Test Signer",
        },
        "source": {
            key: capture_source[key]
            for key in (
                "actor",
                "ref",
                "repository",
                "runAttempt",
                "runId",
                "sha",
                "workflow",
            )
        },
        "status": "verified",
        "timestamp": {
            "attributeOid": "1.2.840.113549.1.9.16.2.14",
            "certificateSha256": "3" * 64,
            "chain": dict(chain),
            "format": "rfc3161",
            "generatedAtUtc": timestamp,
            "issuer": "CN=Test Root",
            "messageImprintAlgorithmOid": "2.16.840.1.101.3.4.2.1",
            "messageImprintSha256": "4" * 64,
            "notAfterUtc": "2030-01-01T00:00:00.0000000Z",
            "notBeforeUtc": "2025-01-01T00:00:00.0000000Z",
            "serialNumber": "02",
            "status": "verified",
            "subject": "CN=Test TSA",
            "timestampingEkuOid": "1.3.6.1.5.5.7.3.8",
        },
        "verifier": {
            "implementation": scope.AUTHENTICODE_VERIFIER_RELATIVE_PATH,
            "implementationSha256": digest(
                ROOT / scope.AUTHENTICODE_VERIFIER_RELATIVE_PATH
            ),
            "platform": "windows",
            "powershellVersion": "7.4.0",
        },
    }
    receipt_path = evidence_root / scope.AUTHENTICODE_VERIFICATION_RELATIVE_PATH
    write_json(receipt_path, receipt)
    return {
        "binding": {
            "path": scope.AUTHENTICODE_VERIFICATION_RELATIVE_PATH,
            "sha256": digest(receipt_path),
            "signerCertificateSha256": "1" * 64,
            "signerSpkiSha256": "2" * 64,
            "sizeBytes": receipt_path.stat().st_size,
            "timestampUtc": timestamp,
        },
        "captureSource": capture_source,
    }


def write_native_composite_evidence(
    evidence_root: Path,
    proposal: dict[str, object],
    approval_path: Path,
    approval: dict[str, object],
    authenticode: dict[str, object],
) -> tuple[Path, Path]:
    installer = next(
        row
        for row in proposal["publicationDeltaTuples"]
        if row["artifactRole"] == "installer"
    )
    payload = next(
        row
        for row in proposal["publicationDeltaTuples"]
        if row["artifactRole"] == "payload"
    )
    version = proposal["release"]["version"]
    capture_source = authenticode["captureSource"]
    wrapper_auth = authenticode["binding"]
    raw_auth = {
        **wrapper_auth,
        "path": (
            "authenticode/"
            "AUTHENTICODE_VERIFICATION-avalonia-win-x64.generated.json"
        ),
    }
    reviewer = approval["approver"]
    finalization_source = {
        "actor": reviewer,
        "artifactName": "windows-native-evidence-finalized-13000-1",
        "ref": capture_source["ref"],
        "repository": capture_source["repository"],
        "runAttempt": "1",
        "runId": "13000",
        "sha": capture_source["sha"],
        "workflow": scope.NATIVE_FINALIZATION_WORKFLOW,
    }
    capture_inventory_sha = "5" * 64
    screenshot_rows = []
    for role, contents in (
        ("progress", b"native-progress-screenshot"),
        ("completion", b"native-completion-screenshot"),
    ):
        relative = f"screenshots/windows-installer-avalonia-win-x64-{role}.png"
        screenshot_path = evidence_root / "proof/windows-native" / relative
        screenshot_sha, _ = write_bytes(
            screenshot_path.parent, screenshot_path.name, contents
        )
        screenshot_rows.append(
            {
                "height": 720,
                "path": relative,
                "role": role,
                "sha256": screenshot_sha,
                "width": 1280,
            }
        )

    def artifact_binding(row: dict[str, object]) -> dict[str, object]:
        return {
            "fileName": row["fileName"],
            "relativePath": f"files/{row['fileName']}",
            "sha256": row["sha256"],
            "sizeBytes": row["sizeBytes"],
        }

    capture = {
        "authenticodeVerification": raw_auth,
        "candidate": {"actor": "producer"},
        "captureMode": "interactive",
        "channelId": "preview",
        "contractName": scope.NATIVE_CAPTURE_CONTRACT_NAME,
        "contractVersion": scope.NATIVE_CAPTURE_CONTRACT_VERSION,
        "generatedAt": "2026-07-21T16:30:00Z",
        "heads": [
            {
                "authenticodeVerification": raw_auth,
                "headId": "avalonia",
                "installer": artifact_binding(installer),
                "payload": artifact_binding(payload),
                "progressLog": {
                    "path": "startup-smoke/windows-installer-progress-avalonia-win-x64.log",
                    "sha256": "6" * 64,
                },
                "receipt": {
                    "path": "startup-smoke/startup-smoke-avalonia-win-x64.receipt.json",
                    "sha256": "7" * 64,
                },
                "rid": "win-x64",
                "screenshots": screenshot_rows,
            }
        ],
        "source": capture_source,
        "status": "captured",
        "version": version,
    }
    write_json(evidence_root / scope.NATIVE_CAPTURE_RELATIVE_PATH, capture)

    producer_visual = {
        "artifactDigest": f"sha256:{installer['sha256']}",
        "artifactFileName": installer["fileName"],
        "authenticodeVerification": raw_auth,
        "captureBinding": {
            **{
                key: value
                for key, value in capture_source.items()
                if key != "actor"
            },
            "inventorySha256": capture_inventory_sha,
        },
        "channel": "preview",
        "channelId": "preview",
        "checks": {
            "capture_mode": "interactive",
            "human_review_confirmed": True,
        },
        "clippingReview": {"reviewer": reviewer, "status": "passed"},
        "contractName": scope.WINDOWS_VISUAL_PROOF_CONTRACT_NAME,
        "contractVersion": scope.WINDOWS_VISUAL_PROOF_CONTRACT_VERSION,
        "contrastReview": {"reviewer": reviewer, "status": "passed"},
        "finalizationBinding": finalization_source,
        "generatedAt": "2026-07-21T16:45:00Z",
        "head": "avalonia",
        "headId": "avalonia",
        "platform": "windows",
        "readabilityReview": {"reviewer": reviewer, "status": "passed"},
        "releaseVersion": version,
        "review": {
            "allowlistSource": "repository variable plus protected environment",
            "authenticatedReviewer": reviewer,
            "captureActor": capture_source["actor"],
            "explicitConfirmations": {
                "clipping": "passed",
                "contrast": "passed",
                "readability": "passed",
            },
        },
        "rid": "win-x64",
        "screenshots": [
            {key: row[key] for key in ("path", "role", "sha256")}
            for row in screenshot_rows
        ],
        "status": "passed",
        "version": version,
    }
    producer_visual_path = (
        evidence_root
        / "proof/windows-native"
        / scope.WINDOWS_VISUAL_PROOF_RELATIVE_PATH
    )
    write_json(producer_visual_path, producer_visual)

    raw_scope = {
        "approver": reviewer,
        "path": "PREVIEW_NIGHTLY_PUBLICATION_SCOPE_APPROVAL.generated.json",
        "scopeDecisionSha256": proposal["scopeDecisionSha256"],
        "sha256": digest(approval_path),
    }
    finalization = {
        "authenticodeVerification": raw_auth,
        "captureInventorySha256": capture_inventory_sha,
        "captureSource": capture_source,
        "contractName": scope.NATIVE_FINALIZATION_CONTRACT_NAME,
        "contractVersion": scope.NATIVE_FINALIZATION_CONTRACT_VERSION,
        "finalizationSource": finalization_source,
        "generatedAt": "2026-07-21T16:45:00Z",
        "humanReviewConfirmed": True,
        "proofs": [
            {
                "headId": "avalonia",
                "path": scope.WINDOWS_VISUAL_PROOF_RELATIVE_PATH,
                "sha256": digest(producer_visual_path),
            }
        ],
        "reviewer": reviewer,
        "reviewerWasCaptureActor": False,
        "scopeApproval": raw_scope,
        "status": "passed",
    }
    nested_finalization_path = evidence_root / scope.NATIVE_FINALIZATION_SOURCE_RELATIVE_PATH
    write_json(nested_finalization_path, finalization)
    finalization_path = evidence_root / scope.NATIVE_FINALIZATION_RELATIVE_PATH
    shutil.copy2(nested_finalization_path, finalization_path)

    portable_visual = {
        **producer_visual,
        "authenticodeVerification": wrapper_auth,
        "screenshots": [
            {
                "path": f"proof/windows-native/{row['path']}",
                "role": row["role"],
                "sha256": row["sha256"],
            }
            for row in screenshot_rows
        ],
    }
    visual_path = evidence_root / scope.WINDOWS_VISUAL_PROOF_RELATIVE_PATH
    write_json(visual_path, portable_visual)

    native_path = evidence_root / scope.NATIVE_EVIDENCE_RELATIVE_PATH
    write_json(
        native_path,
        {
            "archivePath": "proof/windows-native-finalized.zip",
            "archiveSha256": "8" * 64,
            "authenticodeVerification": wrapper_auth,
            "candidateProvenance": {
                "candidate": {
                    "actor": "producer",
                    "installerSha256": installer["sha256"],
                    "payloadSha256": payload["sha256"],
                }
            },
            "captureInventorySha256": capture_inventory_sha,
            "captureSource": capture_source,
            "contractName": scope.NATIVE_EVIDENCE_CONTRACT_NAME,
            "contractVersion": scope.NATIVE_EVIDENCE_CONTRACT_VERSION,
            "fileCount": 8,
            "finalizationSha256": digest(finalization_path),
            "finalizationSource": finalization_source,
            "finalizedInventorySha256": "9" * 64,
            "githubActionsProvenance": {"status": "completed"},
            "nativeFinalization": {
                "path": scope.NATIVE_FINALIZATION_RELATIVE_PATH,
                "sha256": digest(finalization_path),
                "sizeBytes": finalization_path.stat().st_size,
            },
            "progressLogSha256": {"avalonia": "a" * 64},
            "release": {"channel": "preview", "version": version},
            "scopeApproval": {**raw_scope, "payload": approval},
            "startupReceiptSha256": {"avalonia": "b" * 64},
            "status": "passed",
            "treeSha256": "c" * 64,
            "visualProof": {
                "path": scope.WINDOWS_VISUAL_PROOF_RELATIVE_PATH,
                "sha256": digest(visual_path),
                "sizeBytes": visual_path.stat().st_size,
            },
            "visualProofSha256": {"avalonia": digest(visual_path)},
            "visualReviewers": {"avalonia": reviewer},
        },
    )
    return native_path, visual_path


def finalize_for_test(
    tmp_path: Path, values: dict[str, object], proposal: dict[str, object]
) -> Path:
    evidence_root = values["evidence_root"]
    approval_path = (
        evidence_root
        / "proof/windows-native/PREVIEW_NIGHTLY_PUBLICATION_SCOPE_APPROVAL.generated.json"
    )
    final_path = evidence_root / scope.FINAL_FILE_NAME
    authenticode = write_authenticode_evidence(evidence_root, proposal)
    approval = approval_for(
        proposal,
        values["paths"]["output"],
        authenticode_sha256=authenticode["binding"]["sha256"],
    )
    write_json(approval_path, approval)
    native_path, visual_path = write_native_composite_evidence(
        evidence_root,
        proposal,
        approval_path,
        approval,
        authenticode,
    )
    signing_path = evidence_root / scope.SIGNING_RECEIPT_RELATIVE_PATH
    signing_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(values["paths"]["signing_receipt"], signing_path)
    scope.finalize_scope(
        argparse.Namespace(
            proposal=values["paths"]["output"],
            approval=approval_path,
            approval_receipt_path=approval_path.relative_to(evidence_root).as_posix(),
            native_evidence=native_path,
            visual_approval=[visual_path],
            disallowed_actor=["producer", "capture-bot"],
            output=final_path,
        )
    )
    return final_path


def test_finalize_binds_native_visual_and_independent_exact_decision(tmp_path: Path) -> None:
    values, proposal = prepare(tmp_path)
    proposal_path = values["paths"]["output"]
    final_path = finalize_for_test(tmp_path, values, proposal)
    final = json.loads(final_path.read_text(encoding="utf-8"))
    assert final["status"] == "validated"
    assert final["approvalIndependent"] is True
    assert final["publicationEligible"] is False
    assert final["registryFinalizeEligible"] is True
    assert final["uploadAuthorized"] is False
    scope.verify_scope(
        argparse.Namespace(
            scope=final_path,
            proposal=proposal_path,
            publication_dir=values["paths"]["publication_dir"],
            evidence_root=values["evidence_root"],
        )
    )


@pytest.mark.parametrize(
    "reference_name",
    (
        "wrapper",
        "nativeFinalization",
        "visualProof",
        "authenticodeVerification",
    ),
)
@pytest.mark.parametrize(
    ("field", "shape"),
    (
        ("contractVersion", "bool"),
        ("contractVersion", "float"),
        ("sizeBytes", "bool"),
        ("sizeBytes", "float"),
    ),
)
def test_native_composite_references_require_exact_json_integers(
    tmp_path: Path,
    reference_name: str,
    field: str,
    shape: str,
) -> None:
    values, proposal = prepare(tmp_path)
    final_path = finalize_for_test(tmp_path, values, proposal)
    final = json.loads(final_path.read_text(encoding="utf-8"))
    composite = final["nativeEvidenceComposite"]
    exact = composite[reference_name][field]
    composite[reference_name][field] = True if shape == "bool" else float(exact)

    with pytest.raises(scope.ScopeError, match="integer|contract identity"):
        scope.validate_native_evidence_composite_binding(composite)


@pytest.mark.parametrize(
    "reference_name",
    (
        "wrapper",
        "nativeFinalization",
        "visualProof",
        "authenticodeVerification",
    ),
)
@pytest.mark.parametrize(
    "mutation",
    ("contract_name", "path", "sha256", "extra_key"),
)
def test_native_composite_references_require_exact_names_paths_hashes_and_keys(
    tmp_path: Path,
    reference_name: str,
    mutation: str,
) -> None:
    values, proposal = prepare(tmp_path)
    final_path = finalize_for_test(tmp_path, values, proposal)
    final = json.loads(final_path.read_text(encoding="utf-8"))
    reference = final["nativeEvidenceComposite"][reference_name]
    if mutation == "contract_name":
        reference["contractName"] += ".forged"
    elif mutation == "path":
        reference["path"] = f"./{reference['path']}"
    elif mutation == "sha256":
        reference["sha256"] = reference["sha256"].upper()
    else:
        reference["unexpected"] = None

    with pytest.raises(scope.ScopeError):
        scope.validate_native_evidence_composite_binding(
            final["nativeEvidenceComposite"]
        )


@pytest.mark.parametrize(
    "mutation",
    (
        "finalization_contract",
        "finalization_contract_version",
        "capture_contract",
        "capture_contract_version",
        "candidate_reviewer_same_actor",
        "visual_contract",
        "visual_contract_version",
        "visual_platform",
        "visual_head",
        "visual_rid",
        "capture_release",
        "visual_release",
        "visual_checks",
    ),
)
def test_native_composite_rejects_resealed_semantic_forgery(
    tmp_path: Path,
    mutation: str,
) -> None:
    values, proposal = prepare(tmp_path)
    finalize_for_test(tmp_path, values, proposal)
    evidence_root = values["evidence_root"]
    native_path = evidence_root / scope.NATIVE_EVIDENCE_RELATIVE_PATH
    visual_path = evidence_root / scope.WINDOWS_VISUAL_PROOF_RELATIVE_PATH
    wrapper = json.loads(native_path.read_text(encoding="utf-8"))

    if mutation.startswith("finalization_"):
        nested_path = evidence_root / scope.NATIVE_FINALIZATION_SOURCE_RELATIVE_PATH
        finalization = json.loads(nested_path.read_text(encoding="utf-8"))
        if mutation == "finalization_contract":
            finalization["contractName"] = "forged.native-finalization"
        else:
            finalization["contractVersion"] = 1
        write_json(nested_path, finalization)
        root_path = evidence_root / scope.NATIVE_FINALIZATION_RELATIVE_PATH
        shutil.copy2(nested_path, root_path)
        wrapper["finalizationSha256"] = digest(root_path)
        wrapper["nativeFinalization"] = {
            "path": scope.NATIVE_FINALIZATION_RELATIVE_PATH,
            "sha256": digest(root_path),
            "sizeBytes": root_path.stat().st_size,
        }
    elif mutation.startswith("capture_"):
        capture_path = evidence_root / scope.NATIVE_CAPTURE_RELATIVE_PATH
        capture = json.loads(capture_path.read_text(encoding="utf-8"))
        if mutation == "capture_contract":
            capture["contractName"] = "forged.native-capture"
        elif mutation == "capture_contract_version":
            capture["contractVersion"] = 1
        else:
            capture["version"] = "run-forged-release"
        write_json(capture_path, capture)
    elif mutation == "candidate_reviewer_same_actor":
        capture_path = evidence_root / scope.NATIVE_CAPTURE_RELATIVE_PATH
        capture = json.loads(capture_path.read_text(encoding="utf-8"))
        reviewer = wrapper["scopeApproval"]["approver"]
        capture["candidate"]["actor"] = reviewer
        wrapper["candidateProvenance"]["candidate"]["actor"] = reviewer
        write_json(capture_path, capture)
    else:
        visual = json.loads(visual_path.read_text(encoding="utf-8"))
        if mutation == "visual_contract":
            visual["contractName"] = "forged.visual-proof"
        elif mutation == "visual_contract_version":
            visual["contractVersion"] = 2
        elif mutation == "visual_platform":
            visual["platform"] = "linux"
        elif mutation == "visual_head":
            visual["head"] = visual["headId"] = "wpf"
        elif mutation == "visual_rid":
            visual["rid"] = "win-arm64"
        elif mutation == "visual_release":
            visual["version"] = visual["releaseVersion"] = "run-forged-release"
        else:
            visual["checks"]["capture_mode"] = "automated"
        write_json(visual_path, visual)
        wrapper["visualProof"] = {
            "path": scope.WINDOWS_VISUAL_PROOF_RELATIVE_PATH,
            "sha256": digest(visual_path),
            "sizeBytes": visual_path.stat().st_size,
        }
        wrapper["visualProofSha256"] = {"avalonia": digest(visual_path)}

    write_json(native_path, wrapper)
    with pytest.raises(scope.ScopeError):
        scope._validate_native_wrapper_and_documents(
            native_path,
            [visual_path],
            proposal,
        )


@pytest.mark.parametrize(
    ("field", "value"),
    (("publicationEligible", True), ("registryFinalizeEligible", False)),
)
def test_approved_ui_scope_cannot_claim_publication_or_skip_finalize_eligibility(
    tmp_path: Path, field: str, value: bool
) -> None:
    values, proposal = prepare(tmp_path)
    final_path = finalize_for_test(tmp_path, values, proposal)
    final = json.loads(final_path.read_text(encoding="utf-8"))
    final[field] = value

    with pytest.raises(
        scope.ScopeError,
        match="Registry FINALIZE|publication remains fail-closed",
    ):
        scope.validate_proposal(final)


def test_approval_independence_and_exact_digest_tamper_are_rejected(tmp_path: Path) -> None:
    values, proposal = prepare(tmp_path)
    proposal_path = values["paths"]["output"]
    same_actor = approval_for(proposal, proposal_path, "producer")
    with pytest.raises(scope.ScopeError, match="not independent"):
        scope.validate_approval(
            same_actor, proposal, digest(proposal_path), "a" * 64, ["producer"]
        )
    tampered = approval_for(proposal, proposal_path)
    tampered["fullShelfManifestSha256"] = "a" * 64
    with pytest.raises(scope.ScopeError, match="fullShelfManifestSha256"):
        scope.validate_approval(
            tampered, proposal, digest(proposal_path), "a" * 64, ["producer"]
        )
    with pytest.raises(scope.ScopeError, match="authenticodeVerificationSha256"):
        scope.validate_approval(
            approval_for(proposal, proposal_path),
            proposal,
            digest(proposal_path),
            "b" * 64,
            ["producer"],
        )


@pytest.mark.parametrize(
    ("path", "value"),
    (
        (("artifact", "sha256"), "0" * 64),
        (("signature", "status"), "unsigned"),
        (("signer", "chain", "trusted"), False),
        (("timestamp", "status"), "missing"),
        (("timestamp", "generatedAtUtc"), "2031-01-01T00:00:00.0000000Z"),
        (("verifier", "implementationSha256"), "0" * 64),
    ),
)
def test_scope_revalidates_native_authenticode_semantics_after_digest_rebinding(
    tmp_path: Path,
    path: tuple[str, ...],
    value: object,
) -> None:
    values, proposal = prepare(tmp_path)
    evidence_root = values["evidence_root"]
    authenticode = write_authenticode_evidence(evidence_root, proposal)
    receipt_path = evidence_root / scope.AUTHENTICODE_VERIFICATION_RELATIVE_PATH
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    target = receipt
    for key in path[:-1]:
        target = target[key]
    target[path[-1]] = value
    write_json(receipt_path, receipt)
    authenticode["binding"]["sha256"] = digest(receipt_path)
    authenticode["binding"]["sizeBytes"] = receipt_path.stat().st_size
    native = {
        "authenticodeVerification": authenticode["binding"],
        "captureSource": authenticode["captureSource"],
    }
    installers = [
        row
        for row in proposal["publicationDeltaTuples"]
        if row["artifactRole"] == "installer"
    ]

    with pytest.raises(scope.ScopeError, match="Authenticode|RFC3161|timestamp"):
        scope.validate_native_authenticode(native, evidence_root, installers)


def test_approval_cannot_replay_across_compatibility_shelves(tmp_path: Path) -> None:
    first_root = tmp_path / "first"
    second_root = tmp_path / "second"
    first_root.mkdir()
    second_root.mkdir()
    first_values, first = prepare(first_root)
    first_path = first_values["paths"]["output"]
    approval = approval_for(first, first_path)

    second_values = fixture(second_root)
    compatibility_path = second_values["paths"]["incumbent_releases"]
    compatibility_payload = json.loads(compatibility_path.read_text())
    compatibility_payload["status"] = "different-exact-compatibility-state"
    write_json(compatibility_path, compatibility_payload)
    second = scope.prepare_scope(second_values["args"])
    second_path = second_values["paths"]["output"]

    assert first["fullShelfManifestSha256"] == second["fullShelfManifestSha256"]
    assert first["fullShelfCompatibilityManifestSha256"] != second[
        "fullShelfCompatibilityManifestSha256"
    ]
    with pytest.raises(scope.ScopeError):
        scope.validate_approval(
            approval,
            second,
            digest(second_path),
            "a" * 64,
            [],
        )


@pytest.mark.parametrize("mutation", ["aur_bytes", "ancillary_mode"])
def test_approval_cannot_replay_across_ancillary_bytes_or_modes(
    tmp_path: Path, mutation: str
) -> None:
    first_root = tmp_path / "first"
    second_root = tmp_path / "second"
    first_root.mkdir()
    second_root.mkdir()
    first_values, first = prepare(first_root)
    approval = approval_for(first, first_values["paths"]["output"])

    second_values = fixture(second_root)
    if mutation == "aur_bytes":
        (second_values["incumbent_shelf"] / "aur-packages.json").write_bytes(
            b"different-approved-aur-index"
        )
    else:
        (
            second_values["incumbent_shelf"]
            / "files/chummer6-bin.PKGBUILD"
        ).chmod(0o700)
    second = scope.prepare_scope(second_values["args"])

    assert first["fullShelfManifestSha256"] == second["fullShelfManifestSha256"]
    assert first["fullShelfInventorySha256"] != second["fullShelfInventorySha256"]
    with pytest.raises(scope.ScopeError):
        scope.validate_approval(
            approval,
            second,
            digest(second_values["paths"]["output"]),
            "a" * 64,
            [],
        )


@pytest.mark.parametrize(
    "mutation", ["symlink", "fifo", "hardlink", "case_collision", "mode"]
)
def test_sealed_full_source_rejects_entry_or_mode_drift(
    tmp_path: Path, mutation: str
) -> None:
    values, proposal = prepare(tmp_path)
    final = finalize_for_test(tmp_path, values, proposal)
    retained = values["args"].incumbent_snapshot_dir
    note = retained / "operator-note.txt"
    if mutation == "symlink":
        note.unlink()
        note.symlink_to("aur-packages.json")
    elif mutation == "fifo":
        note.unlink()
        os.mkfifo(note)
    elif mutation == "hardlink":
        os.link(note, retained / "operator-note-alias.txt")
    elif mutation == "case_collision":
        (retained / "Case.txt").write_bytes(b"first")
        (retained / "case.TXT").write_bytes(b"second")
    else:
        note.chmod(0o600)
    with pytest.raises(scope.ScopeError):
        scope.verify_scope(
            argparse.Namespace(
                scope=final,
                proposal=values["paths"]["output"],
                publication_dir=values["paths"]["publication_dir"],
                evidence_root=values["evidence_root"],
            )
        )


def test_publication_verify_rejects_retained_byte_drift_and_linux_leak(tmp_path: Path) -> None:
    values, proposal = prepare(tmp_path)
    final = finalize_for_test(tmp_path, values, proposal)
    public = values["paths"]["publication_dir"]
    mac = public / "files" / "chummer-avalonia-osx-arm64-installer.dmg"
    mac.write_bytes(b"drift")
    with pytest.raises(scope.ScopeError, match="inventory.*changed"):
        scope.verify_scope(
            argparse.Namespace(
                scope=final,
                proposal=values["paths"]["output"],
                publication_dir=public,
                evidence_root=values["evidence_root"],
            )
        )
