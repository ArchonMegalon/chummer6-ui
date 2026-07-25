from __future__ import annotations

import argparse
import binascii
import hashlib
import importlib.util
import json
import os
import re
import shutil
import stat
import struct
import subprocess
import sys
import zipfile
import zlib
from pathlib import Path

import pytest
import yaml


REPO_ROOT = Path(__file__).resolve().parents[1]


def load_evidence_module():
    path = REPO_ROOT / "scripts" / "windows_native_evidence.py"
    spec = importlib.util.spec_from_file_location("windows_native_evidence", path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


evidence = load_evidence_module()


def load_supply_chain_fixture_module():
    path = REPO_ROOT / "tests" / "preview_supply_chain_fixtures.py"
    spec = importlib.util.spec_from_file_location("preview_supply_chain_fixtures_native", path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


SUPPLY_FIXTURES = load_supply_chain_fixture_module()
VERSION = "preview-20260718.1"
CHANNEL = "preview"
CAPTURE_SHA = "a" * 40
CANDIDATE_SHA = CAPTURE_SHA
EXACT_SHA256 = "d" * 64
ARTIFACT_SHA256 = "e" * 64
SIGNER_CERTIFICATE_SHA256 = "1" * 64
SIGNER_SPKI_SHA256 = "2" * 64


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def malformed_digest(value: str, shape: str) -> str:
    if shape == "uppercase":
        return "A" * 64
    if shape == "padded":
        return f"{value} "
    if shape == "prefixed":
        return f"sha256:{value}"
    raise AssertionError(f"unknown digest shape: {shape}")


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def canonical_json(payload: object) -> str:
    return json.dumps(payload, sort_keys=True, separators=(",", ":"))


def mutate_canonical(raw: str, key: str, value: object) -> str:
    payload = json.loads(raw)
    payload[key] = value
    return canonical_json(payload)


def png_chunk(kind: bytes, data: bytes) -> bytes:
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", binascii.crc32(kind + data) & 0xFFFFFFFF)


def write_png(path: Path, rgb: tuple[int, int, int]) -> None:
    width, height = 320, 200
    scanline = b"\x00" + bytes(rgb) * width
    pixels = scanline * height
    payload = (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
        + png_chunk(b"IDAT", zlib.compress(pixels))
        + png_chunk(b"IEND", b"")
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)


def write_authenticode_receipt(
    native: Path,
    installer: Path,
    args: argparse.Namespace,
) -> dict[str, object]:
    timestamp = "2026-01-15T12:00:00.0000000Z"
    chain = {
        "trusted": True,
        "revocationMode": "online",
        "revocationFlag": "entire_chain",
        "verificationFlags": "no_flag",
        "verificationTimeUtc": timestamp,
        "status": [],
    }
    receipt = {
        "contractName": evidence.AUTHENTICODE_CONTRACT,
        "contractVersion": 1,
        "status": "verified",
        "generatedAt": "2026-07-20T12:00:00.0000000Z",
        "artifact": {
            "fileName": installer.name,
            "sha256": digest(installer),
            "sizeBytes": installer.stat().st_size,
        },
        "source": {
            "repository": args.source_repository,
            "workflow": args.source_workflow,
            "runId": args.source_run_id,
            "runAttempt": args.source_run_attempt,
            "ref": args.source_ref,
            "sha": args.source_sha,
            "actor": args.source_actor,
            "triggeringActor": args.source_triggering_actor,
            "rerunPolicy": "same-actor-only",
        },
        "policy": {
            "signerCertificateSha256": SIGNER_CERTIFICATE_SHA256,
            "signerSpkiSha256": SIGNER_SPKI_SHA256,
        },
        "signature": {
            "status": "valid",
            "type": "authenticode",
            "cryptographicVerification": "passed",
            "codeSigningEkuOid": "1.3.6.1.5.5.7.3.3",
        },
        "signer": {
            "certificateSha256": SIGNER_CERTIFICATE_SHA256,
            "spkiSha256": SIGNER_SPKI_SHA256,
            "subject": "CN=Chummer Test Signer",
            "issuer": "CN=Chummer Test Root",
            "serialNumber": "01",
            "notBeforeUtc": "2025-01-01T00:00:00.0000000Z",
            "notAfterUtc": "2030-01-01T00:00:00.0000000Z",
            "chain": dict(chain),
        },
        "timestamp": {
            "status": "verified",
            "format": "rfc3161",
            "attributeOid": "1.2.840.113549.1.9.16.2.14",
            "generatedAtUtc": timestamp,
            "messageImprintAlgorithmOid": "2.16.840.1.101.3.4.2.1",
            "messageImprintSha256": "4" * 64,
            "certificateSha256": "3" * 64,
            "subject": "CN=Chummer Test TSA",
            "issuer": "CN=Chummer Test Root",
            "serialNumber": "02",
            "notBeforeUtc": "2025-01-01T00:00:00.0000000Z",
            "notAfterUtc": "2030-01-01T00:00:00.0000000Z",
            "timestampingEkuOid": "1.3.6.1.5.5.7.3.8",
            "chain": dict(chain),
        },
        "verifier": {
            "implementation": "scripts/verify-windows-authenticode.ps1",
            "implementationSha256": digest(
                REPO_ROOT / "scripts" / "verify-windows-authenticode.ps1"
            ),
            "platform": "windows",
            "powershellVersion": "7.4.0",
        },
    }
    write_json(native / evidence.AUTHENTICODE_FILE, receipt)
    args.expected_authenticode_signer_certificate_sha256 = (
        SIGNER_CERTIFICATE_SHA256
    )
    args.expected_authenticode_signer_spki_sha256 = SIGNER_SPKI_SHA256
    return receipt


def make_fixture(
    root: Path, *, payload_bytes: bytes | None = None
) -> tuple[Path, Path, argparse.Namespace]:
    candidate = root / "candidate"
    native = root / "native-evidence"
    files = candidate / "files"
    files.mkdir(parents=True)
    native.mkdir()
    rows = []
    bindings: dict[str, dict[str, object]] = {}
    for index, head in enumerate(evidence.HEADS, start=1):
        installer_name = f"chummer-{head}-win-x64-installer.exe"
        payload_name = f"chummer-{head}-win-x64-payload.zip"
        installer = files / installer_name
        payload = files / payload_name
        installer.write_bytes(b"MZ" + bytes([index]) * 1024)
        payload.write_bytes(
            payload_bytes
            if payload_bytes is not None
            else b"PK" + bytes([index + 10]) * 2048
        )
        bindings[head] = {
            "installer": f"files/{installer_name}",
            "installer_sha256": digest(installer),
            "payload": f"files/{payload_name}",
            "payload_sha256": digest(payload),
            "payload_size": payload.stat().st_size,
        }
        rows.append(
            {
                "artifactId": f"{head}-win-x64-installer",
                "head": head,
                "platform": "windows",
                "rid": "win-x64",
                "kind": "installer",
                "fileName": installer_name,
                "sha256": digest(installer),
                "sizeBytes": installer.stat().st_size,
                "installerMode": "bootstrap",
                "payloadAcquisitionMode": "download",
                "payloadFileName": payload_name,
                "payloadSha256": digest(payload),
                "payloadSizeBytes": payload.stat().st_size,
            }
        )
        paths = evidence.head_paths(head)
        write_json(
            native / paths["receipt"],
            {
                "status": "pass",
                "readyCheckpoint": "pre_ui_event_loop",
                "headId": head,
                "platform": "windows",
                "rid": "win-x64",
                "channelId": CHANNEL,
                "releaseVersion": VERSION,
                "artifactFileName": installer_name,
                "artifactDigest": f"sha256:{digest(installer)}",
                "bootstrapPayloadAcquisitionMode": "download",
                "bootstrapPayloadFileName": payload_name,
                "bootstrapPayloadSha256": digest(payload),
                "bootstrapPayloadSizeBytes": payload.stat().st_size,
                "executionEnvironment": "native_windows",
                "nativeHostEvidence": {
                    "contractName": evidence.NATIVE_HOST_CONTRACT,
                    "status": "verified",
                    "isNativeWindows": True,
                    "hostPlatform": "windows",
                    "hostKernel": "MINGW64_NT-10.0",
                    "runner": "powershell.exe",
                    "evidenceSource": "GitHub-hosted windows-latest",
                },
            },
        )
        progress = native / paths["progressLog"]
        progress.parent.mkdir(parents=True, exist_ok=True)
        progress.write_text("\n".join(evidence.PROGRESS_MARKERS) + "\n", encoding="utf-8")
        write_png(native / paths["progressScreenshot"], (10 * index, 30, 60))
        write_png(native / paths["completionScreenshot"], (10 * index, 130, 160))
    rows.append(
        {
            "artifactId": "avalonia-linux-x64-installer",
            "head": "avalonia",
            "platform": "linux",
            "rid": "linux-x64",
            "kind": "installer",
            "fileName": "chummer-avalonia-linux-x64-installer.deb",
            "sha256": "f" * 64,
            "sizeBytes": 1,
        }
    )
    manifest = candidate / "RELEASE_CHANNEL.generated.json"
    write_json(
        manifest,
        {
            "contractName": evidence.CANDIDATE_MANIFEST_CONTRACT,
            "contract_name": evidence.CANDIDATE_MANIFEST_CONTRACT,
            "schemaVersion": 1,
            "version": VERSION,
            "releaseVersion": VERSION,
            "channelId": CHANNEL,
            "channel": CHANNEL,
            "desktopTupleCoverage": {
                "requiredDesktopHeads": list(evidence.HEADS),
                "requiredDesktopPlatforms": list(
                    evidence.ACTIVE_PREVIEW_DESKTOP_PLATFORMS
                ),
            },
            "artifacts": rows,
        },
    )
    SUPPLY_FIXTURES.write_valid_supply_chain(
        candidate,
        version=VERSION,
        source_commit=CANDIDATE_SHA,
        supply=evidence.SUPPLY_CHAIN,
    )
    content_rows = [
        {
            "path": relative,
            "sha256": digest(candidate / relative),
            "sizeBytes": (candidate / relative).stat().st_size,
        }
        for relative in sorted(evidence.CANDIDATE_CONTENT_PATHS)
    ]
    inventory = {
        "contractName": evidence.CANDIDATE_INVENTORY_CONTRACT,
        "contractVersion": 1,
        "release": {"channel": CHANNEL, "version": VERSION},
        "manifest": {"path": manifest.name, "sha256": digest(manifest)},
        "files": content_rows,
    }
    inventory_path = candidate / evidence.CANDIDATE_INVENTORY_FILE
    write_json(inventory_path, inventory)
    receipt_heads = []
    for head in evidence.HEADS:
        receipt_heads.append(
            {
                "headId": head,
                "rid": "win-x64",
                "installer": {
                    "relativePath": bindings[head]["installer"],
                    "fileName": Path(bindings[head]["installer"]).name,
                    "sha256": bindings[head]["installer_sha256"],
                    "sizeBytes": (candidate / str(bindings[head]["installer"])).stat().st_size,
                },
                "payload": {
                    "relativePath": bindings[head]["payload"],
                    "fileName": Path(bindings[head]["payload"]).name,
                    "sha256": bindings[head]["payload_sha256"],
                    "sizeBytes": (candidate / str(bindings[head]["payload"])).stat().st_size,
                },
            }
        )
    producer = {
        "repository": "ArchonMegalon/chummer6-ui",
        "workflow": evidence.PRODUCER_WORKFLOW,
        "runId": "12000",
        "runAttempt": "1",
        "ref": evidence.PRODUCER_REF,
        "sha": CANDIDATE_SHA,
        "actor": "capture-operator",
        "artifactName": "preview-nightly-candidate-12000-1",
        "runnerLabel": "chummer-preview-nightly-export-abcdefghijkl",
    }
    receipt_path = candidate / evidence.CANDIDATE_EXPORT_FILE
    write_json(
        receipt_path,
        {
            "contractName": evidence.CANDIDATE_EXPORT_CONTRACT,
            "contractVersion": 1,
            "status": "exported",
            "release": inventory["release"],
            "source": producer,
            "candidateManifest": inventory["manifest"],
            "contentInventory": {
                "path": inventory_path.name,
                "sha256": digest(inventory_path),
            },
            "heads": receipt_heads,
            "supplyChain": evidence.SUPPLY_CHAIN.content_bindings(candidate),
            "supplyChainVerification": {
                "mode": evidence.SUPPLY_CHAIN.LIVE_VERIFICATION_MODE,
                "releaseAuthoritative": True,
            },
        },
    )
    handoff = {
        "actor": producer["actor"],
        "artifactId": "777",
        "artifactName": producer["artifactName"],
        "artifactSha256": ARTIFACT_SHA256,
        "contentInventorySha256": digest(inventory_path),
        "contractName": evidence.CANDIDATE_HANDOFF_CONTRACT,
        "contractVersion": 1,
        "ref": producer["ref"],
        "repository": producer["repository"],
        "runAttempt": producer["runAttempt"],
        "runId": producer["runId"],
        "sha": producer["sha"],
        "workflow": producer["workflow"],
    }
    api = {
        "actor": producer["actor"],
        "artifactCreatedAt": "2026-07-18T00:00:00Z",
        "artifactExpiresAt": "2999-07-18T00:00:00Z",
        "artifactId": handoff["artifactId"],
        "artifactName": handoff["artifactName"],
        "artifactSha256": handoff["artifactSha256"],
        "conclusion": "success",
        "contractName": evidence.CANDIDATE_API_CONTRACT,
        "contractVersion": 1,
        "event": "workflow_dispatch",
        "ref": handoff["ref"],
        "repository": handoff["repository"],
        "runAttempt": handoff["runAttempt"],
        "runId": handoff["runId"],
        "sha": handoff["sha"],
        "status": "completed",
        "workflow": handoff["workflow"],
    }
    args = argparse.Namespace(
        candidate_root=candidate,
        candidate_handoff_json=canonical_json(handoff),
        candidate_api_json=canonical_json(api),
        evidence_root=native,
        source_repository=producer["repository"],
        source_workflow=".github/workflows/windows-native-evidence-capture.yml",
        source_run_id="12345",
        source_run_attempt="1",
        source_ref=evidence.PRODUCER_REF,
        source_sha=CAPTURE_SHA,
        source_actor="github-actions[bot]",
        source_triggering_actor="github-actions[bot]",
        output_artifact_name="windows-native-evidence-12345-1",
    )
    return candidate, native, args


def upgrade_fixture_to_windows_only_scope(
    root: Path,
    candidate: Path,
    args: argparse.Namespace,
    *,
    incumbent_platforms: frozenset[str] = frozenset({"macos"}),
) -> dict[str, object]:
    manifest_path = candidate / evidence.CANDIDATE_MANIFEST_FILE
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    for row in manifest["artifacts"]:
        platform = str(row["platform"])
        row["platformId"] = platform
        row["platformLabel"] = {
            "windows": "Windows",
            "linux": "Linux",
            "macos": "macOS",
        }[platform]
        row["arch"] = str(row["rid"]).rsplit("-", 1)[1]
        row["format"] = str(row["fileName"]).rsplit(".", 1)[1]
        row["channelId"] = CHANNEL
        row["version"] = VERSION
        row["downloadUrl"] = f"https://downloads.example/{row['fileName']}"
        if row.get("payloadFileName") is not None:
            row["payloadDownloadUrl"] = (
                f"https://downloads.example/{row['payloadFileName']}"
            )
    linux_row = next(row for row in manifest["artifacts"] if row["platform"] == "linux")
    linux_path = candidate / "files" / linux_row["fileName"]
    linux_path.write_bytes(b"fresh-linux")
    linux_row.update(
        {"sha256": digest(linux_path), "sizeBytes": linux_path.stat().st_size}
    )
    write_json(manifest_path, manifest)
    for relative in evidence.SUPPLY_CHAIN.SUPPLY_CHAIN_CONTENT_PATHS:
        (candidate / relative).unlink()
    for _, _, rid in evidence.SUPPLY_CHAIN.ACTIVE_TUPLES:
        (candidate.parent / f"{candidate.name}-{rid}-project.assets.json").unlink()
    SUPPLY_FIXTURES.write_valid_supply_chain(
        candidate,
        version=VERSION,
        source_commit=CANDIDATE_SHA,
        supply=evidence.SUPPLY_CHAIN,
    )

    build_releases = candidate / "releases.json"
    write_json(
        build_releases,
        {
            "contractName": evidence.CANDIDATE_MANIFEST_CONTRACT,
            "schemaVersion": 1,
            "version": VERSION,
            "channel": CHANNEL,
            "generatedAt": "2026-07-22T00:00:00Z",
            "publishedAt": "2026-07-22T00:00:00Z",
            "downloads": manifest["artifacts"],
        },
    )

    incumbent = root / "incumbent"
    incumbent_files = incumbent / "files"
    incumbent_files.mkdir(parents=True)
    incumbent_rows: list[dict[str, object]] = []
    for platform, rid, suffix, content in (
        ("windows", "win-x64", "installer.exe", b"old-windows"),
        ("linux", "linux-x64", "installer.deb", b"old-linux"),
        ("macos", "osx-arm64", "installer.dmg", b"old-macos"),
    ):
        if platform not in incumbent_platforms:
            continue
        name = f"chummer-avalonia-{rid}-{suffix}"
        path = incumbent_files / name
        path.write_bytes(content)
        row: dict[str, object] = {
            "artifactId": f"avalonia-{rid}-installer",
            "head": "avalonia",
            "platform": platform,
            "rid": rid,
            "kind": "installer",
            "fileName": name,
            "sha256": digest(path),
            "sizeBytes": path.stat().st_size,
            "downloadUrl": f"https://downloads.example/{name}",
            "platformId": platform,
            "platformLabel": {
                "windows": "Windows",
                "linux": "Linux",
                "macos": "macOS",
            }[platform],
            "arch": rid.rsplit("-", 1)[1],
            "format": name.rsplit(".", 1)[1],
            "channelId": CHANNEL,
            "version": "incumbent-1",
        }
        if platform == "windows":
            payload_name = "chummer-avalonia-win-x64-payload.zip"
            payload_path = incumbent_files / payload_name
            payload_path.write_bytes(b"old-payload")
            row.update(
                {
                    "payloadFileName": payload_name,
                    "payloadSha256": digest(payload_path),
                    "payloadSizeBytes": payload_path.stat().st_size,
                    "payloadDownloadUrl": (
                        f"https://downloads.example/{payload_name}"
                    ),
                }
            )
        incumbent_rows.append(row)
    incumbent_manifest = incumbent / evidence.CANDIDATE_MANIFEST_FILE
    incumbent_releases = incumbent / "releases.json"
    write_json(
        incumbent_manifest,
        {
            "contractName": evidence.CANDIDATE_MANIFEST_CONTRACT,
            "schemaVersion": 1,
            "version": "incumbent-1",
            "channelId": CHANNEL,
            "desktopTupleCoverage": {
                "requiredDesktopHeads": list(evidence.HEADS),
                "requiredDesktopPlatforms": sorted(incumbent_platforms),
            },
            "artifacts": incumbent_rows,
        },
    )
    write_json(
        incumbent_releases,
        {
            "contractName": evidence.CANDIDATE_MANIFEST_CONTRACT,
            "schemaVersion": 1,
            "version": "incumbent-1",
            "channel": CHANNEL,
            "generatedAt": "2026-07-21T00:00:00Z",
            "publishedAt": "2026-07-21T00:00:00Z",
            "desktopTupleCoverage": {
                "requiredDesktopHeads": list(evidence.HEADS),
                "requiredDesktopPlatforms": sorted(incumbent_platforms),
            },
            "downloads": incumbent_rows,
        },
    )

    windows_row = next(
        row for row in manifest["artifacts"] if row["platform"] == "windows"
    )
    signing_path = candidate / evidence.PUBLICATION_SCOPE.SIGNING_RECEIPT_RELATIVE_PATH
    write_json(
        signing_path,
        {
            "app": "avalonia",
            "artifacts": [
                {
                    "fileName": windows_row["fileName"],
                    "sha256": windows_row["sha256"],
                    "signingStatus": "pass",
                }
            ],
            "candidateBindings": [
                {
                    "artifactRole": "installer",
                    "authenticodeStatus": "pass",
                    "fileName": windows_row["fileName"],
                    "sha256": windows_row["sha256"],
                    "sizeBytes": windows_row["sizeBytes"],
                },
                {
                    "artifactRole": "payload",
                    "authenticodeStatus": "not_applicable_payload",
                    "fileName": windows_row["payloadFileName"],
                    "sha256": windows_row["payloadSha256"],
                    "sizeBytes": windows_row["payloadSizeBytes"],
                },
            ],
            "contractName": "chummer6-ui.desktop_artifact_signing",
            "contractVersion": 2,
            "platform": "windows",
            "releaseChannel": CHANNEL,
            "releaseVersion": VERSION,
            "rid": "win-x64",
            "signingStatus": "pass",
        },
    )
    proposal_path = candidate / evidence.PUBLICATION_SCOPE.PROPOSAL_FILE_NAME
    proposal = evidence.PUBLICATION_SCOPE.prepare_scope(
        argparse.Namespace(
            build_manifest=manifest_path,
            build_releases=build_releases,
            build_files_dir=candidate / "files",
            incumbent_manifest=incumbent_manifest,
            incumbent_releases=incumbent_releases,
            incumbent_files_dir=incumbent_files,
            incumbent_shelf_dir=incumbent,
            incumbent_snapshot_dir=candidate / "retained-full-source",
            signing_receipt=signing_path,
            consumer_commit=CANDIDATE_SHA,
            build_manifest_receipt_path=evidence.CANDIDATE_MANIFEST_FILE,
            incumbent_manifest_receipt_path=(
                f"retained-source/{evidence.CANDIDATE_MANIFEST_FILE}"
            ),
            publication_dir=candidate / evidence.PUBLICATION_SCOPE.PUBLICATION_DIRECTORY,
            output=proposal_path,
        )
    )
    scope_binding = evidence.PUBLICATION_SCOPE.validate_export_inputs(
        candidate,
        expected_version=VERSION,
        installer_sha256=str(windows_row["sha256"]),
        payload_sha256=str(windows_row["payloadSha256"]),
    )

    shutil.rmtree(candidate / evidence.PUBLICATION_SCOPE.PUBLICATION_DIRECTORY / "files")
    shutil.rmtree(candidate / "retained-full-source")
    shutil.rmtree(candidate / "release-evidence" / "non-published")
    build_releases.unlink()
    linux_path.unlink()

    inventory_path = candidate / evidence.CANDIDATE_INVENTORY_FILE
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    inventory["contractVersion"] = 2
    inventory["manifest"]["sha256"] = digest(manifest_path)
    inventory["files"] = [
        {
            "path": relative,
            "sha256": digest(candidate / relative),
            "sizeBytes": (candidate / relative).stat().st_size,
        }
        for relative in sorted(evidence.WINDOWS_ONLY_CANDIDATE_CONTENT_PATHS)
    ]
    write_json(inventory_path, inventory)

    receipt_path = candidate / evidence.CANDIDATE_EXPORT_FILE
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["contractVersion"] = 2
    receipt["candidateManifest"]["sha256"] = digest(manifest_path)
    receipt["contentInventory"]["sha256"] = digest(inventory_path)
    receipt["publicationScope"] = scope_binding
    receipt["supplyChain"] = evidence.SUPPLY_CHAIN.content_bindings(candidate)
    write_json(receipt_path, receipt)

    handoff = json.loads(args.candidate_handoff_json)
    handoff.update(
        {
            "contentInventorySha256": digest(inventory_path),
            "contractVersion": 2,
            "fullShelfManifestSha256": scope_binding["fullShelfManifest"]["sha256"],
            "fullShelfCompatibilityManifestSha256": scope_binding[
                "fullShelfCompatibilityManifest"
            ]["sha256"],
            "publicationScopeSha256": scope_binding["proposal"]["sha256"],
            "scopeDecisionSha256": scope_binding["scopeDecisionSha256"],
            "signingReceiptSha256": scope_binding["signingReceipt"]["sha256"],
        }
    )
    args.candidate_handoff_json = canonical_json(handoff)
    write_authenticode_receipt(
        root / "native-evidence",
        candidate / "files" / str(windows_row["fileName"]),
        args,
    )
    return proposal


def test_v4_candidate_handoff_binds_live_predecessor_and_signer_authority(
    tmp_path: Path,
) -> None:
    candidate, _, args = make_fixture(tmp_path)
    upgrade_fixture_to_windows_only_scope(tmp_path, candidate, args)
    handoff = json.loads(args.candidate_handoff_json)
    handoff.update(
        {
            "authenticodeSignerCertificateSha256": "7" * 64,
            "authenticodeSignerSpkiSha256": "8" * 64,
            "contractVersion": 4,
            "liveReleaseChannelSha256": "6" * 64,
            "nMinusOneReleaseSha256": "9" * 64,
            "registryPrepareSha256": "a" * 64,
            "selectedTupleSha256": "5" * 64,
        }
    )
    args.candidate_handoff_json = canonical_json(handoff)
    evidence.preflight(args)

    for field in (
        "authenticodeSignerCertificateSha256",
        "authenticodeSignerSpkiSha256",
        "liveReleaseChannelSha256",
        "nMinusOneReleaseSha256",
        "selectedTupleSha256",
    ):
        mutated = dict(handoff)
        mutated[field] = "A" * 64
        args.candidate_handoff_json = canonical_json(mutated)
        with pytest.raises(evidence.ContractError, match=field):
            evidence.preflight(args)


@pytest.mark.parametrize(
    "mutation",
    (
        "missing-coverage",
        "widened-required-heads",
        "blazor-windows-package",
        "blazor-macos-retained",
        "extra-windows-rid",
        "extra-linux-rid",
    ),
)
def test_native_validator_rejects_desktop_scope_widening(
    tmp_path: Path, mutation: str
) -> None:
    candidate, _, _ = make_fixture(tmp_path)
    manifest = json.loads(
        (candidate / evidence.CANDIDATE_MANIFEST_FILE).read_text(encoding="utf-8")
    )
    artifacts = manifest["artifacts"]
    if mutation == "missing-coverage":
        del manifest["desktopTupleCoverage"]
    elif mutation == "widened-required-heads":
        manifest["desktopTupleCoverage"]["requiredDesktopHeads"] = [
            "avalonia",
            "blazor-desktop",
        ]
    else:
        extra = dict(artifacts[0])
        if mutation == "blazor-windows-package":
            extra.update(
                {
                    "head": "blazor-desktop",
                    "kind": "package",
                    "fileName": "chummer-blazor-desktop-win-x64.msix",
                }
            )
        elif mutation == "blazor-macos-retained":
            extra.update(
                {
                    "head": "blazor-desktop",
                    "platform": "macos",
                    "rid": "osx-arm64",
                    "fileName": "chummer-blazor-desktop-osx-arm64.pkg",
                    "publicationState": "retained",
                }
            )
        elif mutation == "extra-windows-rid":
            extra.update(
                {
                    "rid": "win-arm64",
                    "fileName": "chummer-avalonia-win-arm64-installer.exe",
                }
            )
        else:
            extra.update(
                {
                    "platform": "linux",
                    "rid": "linux-arm64",
                    "fileName": "chummer-avalonia-linux-arm64-installer.deb",
                }
            )
        artifacts.append(extra)
    with pytest.raises(evidence.ContractError, match="desktop|Desktop|Windows"):
        evidence.require_exact_desktop_scope(manifest)


def test_native_validator_rejects_avalonia_macos_artifact_outside_current_registry_target(
    tmp_path: Path,
) -> None:
    candidate, _, _ = make_fixture(tmp_path)
    manifest = json.loads(
        (candidate / evidence.CANDIDATE_MANIFEST_FILE).read_text(encoding="utf-8")
    )
    retained = dict(manifest["artifacts"][-1])
    retained.update(
        {
            "artifactId": "avalonia-osx-arm64-installer",
            "platform": "macos",
            "rid": "osx-arm64",
            "fileName": "chummer-avalonia-osx-arm64-installer.pkg",
            "publicationState": "retained",
        }
    )
    manifest["artifacts"].append(retained)
    with pytest.raises(evidence.ContractError, match="outside the active desktop platforms"):
        evidence.require_exact_desktop_scope(manifest)


def test_native_validator_rejects_unknown_platform_artifact_outside_current_registry_target(
    tmp_path: Path,
) -> None:
    candidate, _, _ = make_fixture(tmp_path)
    manifest = json.loads(
        (candidate / evidence.CANDIDATE_MANIFEST_FILE).read_text(encoding="utf-8")
    )
    unknown = dict(manifest["artifacts"][-1])
    unknown.update(
        {
            "artifactId": "avalonia-freebsd-x64-installer",
            "platform": "freebsd",
            "rid": "freebsd-x64",
            "fileName": "chummer-avalonia-freebsd-x64-installer.tar.zst",
        }
    )
    manifest["artifacts"].append(unknown)
    with pytest.raises(evidence.ContractError, match="outside the active desktop platforms"):
        evidence.require_exact_desktop_scope(manifest)


@pytest.mark.parametrize("mutation", ("platform-alias-conflict", "wrong-linux-identity"))
def test_native_validator_rejects_inexact_active_desktop_artifact_identity(
    tmp_path: Path, mutation: str
) -> None:
    candidate, _, _ = make_fixture(tmp_path)
    manifest = json.loads(
        (candidate / evidence.CANDIDATE_MANIFEST_FILE).read_text(encoding="utf-8")
    )
    if mutation == "platform-alias-conflict":
        manifest["artifacts"][0]["platformId"] = "macos"
    else:
        manifest["artifacts"][-1]["artifactId"] = "avalonia-linux-x64-package"
        manifest["artifacts"][-1]["fileName"] = "forged-linux-installer.deb"
    with pytest.raises(evidence.ContractError, match="platform identity|artifact identity"):
        evidence.require_exact_desktop_scope(manifest)


def test_preflight_rejects_digest_rebound_unpromoted_windows_install_media(
    tmp_path: Path,
) -> None:
    candidate, _, args = make_fixture(tmp_path)
    manifest_path = candidate / evidence.CANDIDATE_MANIFEST_FILE
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    extra = dict(manifest["artifacts"][0])
    extra.update(
        {
            "artifactId": "blazor-desktop-win-x64-msix",
            "head": "blazor-desktop",
            "kind": "msix",
            "fileName": "chummer-blazor-desktop-win-x64.msix",
        }
    )
    manifest["artifacts"].append(extra)
    write_json(manifest_path, manifest)

    inventory_path = candidate / evidence.CANDIDATE_INVENTORY_FILE
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    inventory["manifest"]["sha256"] = digest(manifest_path)
    manifest_row = next(
        row
        for row in inventory["files"]
        if row["path"] == evidence.CANDIDATE_MANIFEST_FILE
    )
    manifest_row.update(
        {"sha256": digest(manifest_path), "sizeBytes": manifest_path.stat().st_size}
    )
    write_json(inventory_path, inventory)

    receipt_path = candidate / evidence.CANDIDATE_EXPORT_FILE
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["candidateManifest"]["sha256"] = digest(manifest_path)
    receipt["contentInventory"]["sha256"] = digest(inventory_path)
    write_json(receipt_path, receipt)
    args.candidate_handoff_json = mutate_canonical(
        args.candidate_handoff_json,
        "contentInventorySha256",
        digest(inventory_path),
    )

    with pytest.raises(evidence.ContractError, match="unpromoted desktop head"):
        evidence.preflight(args)


@pytest.mark.parametrize(
    "verification",
    (
        {
            "mode": evidence.SUPPLY_CHAIN.STRUCTURAL_VERIFICATION_MODE,
            "releaseAuthoritative": False,
        },
        {
            "mode": evidence.SUPPLY_CHAIN.LIVE_VERIFICATION_MODE,
            "releaseAuthoritative": 1,
        },
        {
            "mode": evidence.SUPPLY_CHAIN.LIVE_VERIFICATION_MODE,
            "releaseAuthoritative": 0,
        },
    ),
)
def test_preflight_rejects_self_rehashed_nonrelease_supply_chain_claim(
    tmp_path: Path, verification: dict[str, object]
) -> None:
    candidate, _, args = make_fixture(tmp_path)
    receipt_path = candidate / evidence.CANDIDATE_EXPORT_FILE
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["supplyChainVerification"] = verification
    write_json(receipt_path, receipt)

    with pytest.raises(evidence.ContractError, match="release-authoritative|pinned_live"):
        evidence.preflight(args)


def write_candidate_zip(
    archive: Path,
    candidate: Path,
    *,
    replacements: dict[str, tuple[bytes, int]] | None = None,
    renames: dict[str, str] | None = None,
    extra_members: list[tuple[str, bytes, int]] | None = None,
    compression: int = zipfile.ZIP_STORED,
) -> None:
    replacements = replacements or {}
    renames = renames or {}
    extra_members = extra_members or []
    candidate_paths = (
        evidence.WINDOWS_ONLY_CANDIDATE_EXPORT_PATHS
        if (candidate / evidence.PUBLICATION_SCOPE.PROPOSAL_FILE_NAME).is_file()
        else evidence.CANDIDATE_EXPORT_PATHS
    )
    with zipfile.ZipFile(archive, "w", compression=compression, allowZip64=True) as bundle:
        for relative in candidate_paths:
            data, mode = replacements.get(
                relative,
                ((candidate / relative).read_bytes(), stat.S_IFREG | 0o600),
            )
            member = zipfile.ZipInfo(renames.get(relative, relative))
            member.create_system = 3
            member.external_attr = mode << 16
            member.compress_type = compression
            bundle.writestr(member, data)
        for name, data, mode in extra_members:
            member = zipfile.ZipInfo(name)
            member.create_system = 3
            member.external_attr = mode << 16
            member.compress_type = compression
            bundle.writestr(member, data)


def bind_archive_digest(args: argparse.Namespace, archive: Path) -> None:
    archive_sha = digest(archive)
    args.candidate_handoff_json = mutate_canonical(
        args.candidate_handoff_json, "artifactSha256", archive_sha
    )
    args.candidate_api_json = mutate_canonical(
        args.candidate_api_json, "artifactSha256", archive_sha
    )


def make_archive_fixture(
    root: Path,
) -> tuple[Path, Path, Path, argparse.Namespace]:
    candidate, native, args = make_fixture(root)
    archive = root / "candidate.zip"
    write_candidate_zip(archive, candidate)
    bind_archive_digest(args, archive)
    args.candidate_zip = archive
    args.held_root = root / "candidate-held"
    return candidate, native, archive, args


def refresh_capture_inventory(native: Path) -> None:
    inventory_path = native / evidence.CAPTURE_INVENTORY_FILE
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    inventory["captureManifestSha256"] = digest(native / evidence.CAPTURE_FILE)
    inventory["files"] = evidence.exact_inventory(
        native, exclude={evidence.CAPTURE_INVENTORY_FILE}
    )
    write_json(inventory_path, inventory)


def finalize_args(native: Path, output: Path) -> argparse.Namespace:
    return argparse.Namespace(
        capture_root=native,
        output_root=output,
        capture_inventory_sha256=digest(native / evidence.CAPTURE_INVENTORY_FILE),
        expected_repository="ArchonMegalon/chummer6-ui",
        expected_workflow=".github/workflows/windows-native-evidence-capture.yml",
        expected_run_id="12345",
        expected_run_attempt="1",
        expected_ref=evidence.PRODUCER_REF,
        expected_sha=CAPTURE_SHA,
        expected_capture_actor="github-actions[bot]",
        expected_artifact_name="windows-native-evidence-12345-1",
        reviewer_id="accountable-reviewer",
        reviewer_allowlist_json='["accountable-reviewer", "backup-reviewer"]',
        human_review_confirmed="true",
        finalization_repository="ArchonMegalon/chummer6-ui",
        finalization_workflow=evidence.FINALIZE_WORKFLOW,
        finalization_run_id="13000",
        finalization_run_attempt="1",
        finalization_ref=evidence.PRODUCER_REF,
        finalization_sha=CAPTURE_SHA,
        finalization_actor="accountable-reviewer",
        finalization_triggering_actor="accountable-reviewer",
        finalization_artifact_name="windows-native-evidence-finalized-13000-1",
        avalonia_readability="true",
        avalonia_contrast="true",
        avalonia_clipping="true",
        blazor_desktop_readability="true",
        blazor_desktop_contrast="true",
        blazor_desktop_clipping="true",
    )


def windows_only_approval(
    proposal: dict[str, object],
    candidate: Path,
    native: Path,
) -> dict[str, object]:
    return {
        "approvedAt": "2026-07-21T17:00:00Z",
        "approver": "accountable-reviewer",
        "authenticodeVerificationSha256": digest(
            native / evidence.AUTHENTICODE_FILE
        ),
        "contractName": evidence.PUBLICATION_SCOPE.APPROVAL_CONTRACT_NAME,
        "contractVersion": 2,
        "fullShelfCompatibilityManifestSha256": proposal[
            "fullShelfCompatibilityManifestSha256"
        ],
        "fullShelfInventorySha256": proposal["fullShelfInventorySha256"],
        "fullShelfManifestSha256": proposal["fullShelfManifestSha256"],
        "incumbentSnapshotSha256": proposal["incumbentSnapshotSha256"],
        "publicationDeltaSha256": evidence.PUBLICATION_SCOPE.canonical_sha256(
            proposal["publicationDeltaTuples"]
        ),
        "publicationScopeProposalSha256": digest(
            candidate / evidence.PUBLICATION_SCOPE.PROPOSAL_FILE_NAME
        ),
        "registryPrepareSha256": (
            evidence.PUBLICATION_SCOPE.canonical_sha256(
                proposal["registryPrepare"]
            )
            if proposal.get("registryPrepare") is not None
            else None
        ),
        "scopeDecisionSha256": proposal["scopeDecisionSha256"],
        "signingReceiptSha256": proposal["signingReceiptSha256"],
        "status": "approved",
    }


def test_v2_windows_only_capture_and_approval_are_exactly_bound(tmp_path: Path) -> None:
    candidate, native, capture_args = make_fixture(tmp_path)
    proposal = upgrade_fixture_to_windows_only_scope(
        tmp_path, candidate, capture_args
    )
    handoff = json.loads(capture_args.candidate_handoff_json)
    handoff.update(
        {
            "authenticodeSignerCertificateSha256": (
                SIGNER_CERTIFICATE_SHA256
            ),
            "authenticodeSignerSpkiSha256": SIGNER_SPKI_SHA256,
            "contractVersion": 4,
            "liveReleaseChannelSha256": "6" * 64,
            "nMinusOneReleaseSha256": "9" * 64,
            "registryPrepareSha256": "a" * 64,
            "selectedTupleSha256": "5" * 64,
        }
    )
    capture_args.candidate_handoff_json = canonical_json(handoff)

    evidence.capture(capture_args)

    capture = json.loads((native / evidence.CAPTURE_FILE).read_text(encoding="utf-8"))
    inventory = json.loads(
        (native / evidence.CAPTURE_INVENTORY_FILE).read_text(encoding="utf-8")
    )
    assert capture["contractVersion"] == 2
    assert inventory["contractVersion"] == 2
    assert capture["candidate"]["scopeDecisionSha256"] == proposal[
        "scopeDecisionSha256"
    ]
    assert capture["candidate"]["liveReleaseChannelSha256"] == "6" * 64
    assert capture["candidate"]["selectedTupleSha256"] == "5" * 64

    approval = windows_only_approval(proposal, candidate, native)
    finalized = tmp_path / "finalized"
    args = finalize_args(native, finalized)
    args.scope_approval_json = canonical_json(approval)
    evidence.finalize(args)

    finalization = json.loads(
        (finalized / evidence.FINALIZATION_FILE).read_text(encoding="utf-8")
    )
    assert finalization["contractVersion"] == 2
    assert finalization["scopeApproval"]["approver"] == "accountable-reviewer"
    assert finalization["scopeApproval"]["scopeDecisionSha256"] == proposal[
        "scopeDecisionSha256"
    ]
    assert (finalized / evidence.SCOPE_APPROVAL_FILE).is_file()


@pytest.mark.parametrize(
    ("path", "value"),
    (
        (("artifact", "sha256"), "0" * 64),
        (("artifact", "sizeBytes"), 99),
        (("signature", "status"), "unsigned"),
        (("signer", "certificateSha256"), "0" * 64),
        (("signer", "spkiSha256"), "0" * 64),
        (("signer", "chain", "trusted"), False),
        (("timestamp", "chain", "trusted"), False),
        (("timestamp", "status"), "missing"),
        (("timestamp", "format"), "legacy"),
        (("timestamp", "attributeOid"), "1.2.3.4"),
        (("timestamp", "messageImprintAlgorithmOid"), "1.3.14.3.2.26"),
        (("timestamp", "generatedAtUtc"), "2031-01-01T00:00:00.0000000Z"),
        (("source", "sha"), "b" * 40),
        (("source", "workflow"), ".github/workflows/forged.yml"),
        (("policy", "signerCertificateSha256"), "0" * 64),
        (("policy", "signerSpkiSha256"), "0" * 64),
        (("verifier", "implementationSha256"), "0" * 64),
    ),
)
def test_v2_capture_rejects_forged_or_incomplete_authenticode_receipt(
    tmp_path: Path,
    path: tuple[str, ...],
    value: object,
) -> None:
    candidate, native, args = make_fixture(tmp_path)
    upgrade_fixture_to_windows_only_scope(tmp_path, candidate, args)
    receipt_path = native / evidence.AUTHENTICODE_FILE
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    target = receipt
    for key in path[:-1]:
        target = target[key]
    target[path[-1]] = value
    write_json(receipt_path, receipt)

    with pytest.raises(
        evidence.ContractError, match="Authenticode|RFC3161|timestamp|signer"
    ):
        evidence.capture(args)


def test_v2_signing_producer_receipt_cannot_replace_native_authenticode_verification(
    tmp_path: Path,
) -> None:
    candidate, native, args = make_fixture(tmp_path)
    upgrade_fixture_to_windows_only_scope(tmp_path, candidate, args)
    (native / evidence.AUTHENTICODE_FILE).unlink()

    with pytest.raises(evidence.ContractError, match="Authenticode verification receipt"):
        evidence.capture(args)


def test_v2_candidate_rejects_compatibility_manifest_handoff_replay(
    tmp_path: Path,
) -> None:
    candidate, _, args = make_fixture(tmp_path)
    upgrade_fixture_to_windows_only_scope(tmp_path, candidate, args)
    args.candidate_handoff_json = mutate_canonical(
        args.candidate_handoff_json,
        "fullShelfCompatibilityManifestSha256",
        "0" * 64,
    )

    with pytest.raises(
        evidence.ContractError,
        match="fullShelfCompatibilityManifestSha256 differs from publication scope",
    ):
        evidence.validate_candidate_export(args)


def test_v2_complete_shelf_coverage_is_exactly_incumbent_macos_plus_windows(
    tmp_path: Path,
) -> None:
    candidate, _, args = make_fixture(tmp_path)
    proposal = upgrade_fixture_to_windows_only_scope(tmp_path, candidate, args)
    full_manifest = json.loads(
        (
            candidate
            / evidence.PUBLICATION_SCOPE.PUBLICATION_MANIFEST_RELATIVE_PATH
        ).read_text(encoding="utf-8")
    )
    full_compatibility = json.loads(
        (
            candidate
            / evidence.PUBLICATION_SCOPE.PUBLICATION_COMPATIBILITY_MANIFEST_RELATIVE_PATH
        ).read_text(encoding="utf-8")
    )

    evidence.require_complete_windows_only_registry_shelf(
        proposal, full_manifest, full_compatibility
    )
    full_compatibility["desktopTupleCoverage"]["requiredDesktopPlatforms"] = [
        "linux",
        "windows",
    ]
    with pytest.raises(
        evidence.ContractError,
        match="coverage differs from the incumbent-derived public shelf",
    ):
        evidence.require_complete_windows_only_registry_shelf(
            proposal, full_manifest, full_compatibility
        )


def test_automated_capture_and_allowlisted_human_finalize_emit_stage_compatible_proofs(
    tmp_path: Path,
) -> None:
    candidate_root, native, args = make_fixture(tmp_path)
    evidence.capture(args)

    capture_manifest = json.loads((native / evidence.CAPTURE_FILE).read_text(encoding="utf-8"))
    candidate = capture_manifest["candidate"]
    assert set(candidate) == {
        "actor",
        "artifactCreatedAt",
        "artifactExpiresAt",
        "artifactId",
        "artifactName",
        "artifactSha256",
        "authenticatedApiSha256",
        "contentInventory",
        "contentInventorySha256",
        "exportReceipt",
        "exportReceiptSha256",
        "handoffSha256",
        "manifestPath",
        "manifestSha256",
        "ref",
        "repository",
        "runAttempt",
        "runId",
        "sha",
        "supplyChain",
        "workflow",
    }
    assert candidate["artifactId"] == "777"
    assert candidate["artifactSha256"] == ARTIFACT_SHA256
    assert candidate["runAttempt"] == "1"
    assert candidate["ref"] == evidence.PRODUCER_REF
    reconstructed_handoff = {
        key: candidate[key]
        for key in (
            "actor",
            "artifactId",
            "artifactName",
            "artifactSha256",
            "ref",
            "repository",
            "runAttempt",
            "runId",
            "sha",
            "workflow",
        )
    }
    reconstructed_handoff.update(
        {
            "contentInventorySha256": candidate["contentInventorySha256"],
            "contractName": evidence.CANDIDATE_HANDOFF_CONTRACT,
            "contractVersion": 1,
        }
    )
    assert hashlib.sha256(canonical_json(reconstructed_handoff).encode()).hexdigest() == candidate[
        "handoffSha256"
    ]
    reconstructed_api = {
        key: candidate[key]
        for key in (
            "actor",
            "artifactCreatedAt",
            "artifactExpiresAt",
            "artifactId",
            "artifactName",
            "artifactSha256",
            "ref",
            "repository",
            "runAttempt",
            "runId",
            "sha",
            "workflow",
        )
    }
    reconstructed_api.update(
        {
            "conclusion": "success",
            "contractName": evidence.CANDIDATE_API_CONTRACT,
            "contractVersion": 1,
            "event": "workflow_dispatch",
            "status": "completed",
        }
    )
    assert hashlib.sha256(canonical_json(reconstructed_api).encode()).hexdigest() == candidate[
        "authenticatedApiSha256"
    ]
    for key, name in (
        ("contentInventory", evidence.CANDIDATE_INVENTORY_FILE),
        ("exportReceipt", evidence.CANDIDATE_EXPORT_FILE),
    ):
        binding = candidate[key]
        preserved = native / binding["path"]
        assert binding["path"] == f"{evidence.CANDIDATE_PROVENANCE_DIRECTORY}/{name}"
        assert preserved.read_bytes() == (candidate_root / name).read_bytes()
        assert binding["sha256"] == digest(preserved)
    output = tmp_path / "finalized"
    evidence.finalize(finalize_args(native, output))

    assert (output / evidence.CAPTURE_INVENTORY_FILE).is_file()
    assert (output / evidence.FINALIZED_INVENTORY_FILE).is_file()
    for name in (evidence.CANDIDATE_INVENTORY_FILE, evidence.CANDIDATE_EXPORT_FILE):
        assert (
            output / evidence.CANDIDATE_PROVENANCE_DIRECTORY / name
        ).read_bytes() == (candidate_root / name).read_bytes()
    finalized_inventory = json.loads((output / evidence.FINALIZED_INVENTORY_FILE).read_text(encoding="utf-8"))
    assert finalized_inventory["files"] == evidence.exact_inventory(
        output, exclude={evidence.FINALIZED_INVENTORY_FILE}
    )
    finalization = json.loads((output / evidence.FINALIZATION_FILE).read_text(encoding="utf-8"))
    assert finalization["finalizationSource"] == {
        "repository": "ArchonMegalon/chummer6-ui",
        "workflow": evidence.FINALIZE_WORKFLOW,
        "runId": "13000",
        "runAttempt": "1",
        "ref": evidence.PRODUCER_REF,
        "sha": CAPTURE_SHA,
        "actor": "accountable-reviewer",
        "triggeringActor": "accountable-reviewer",
        "rerunPolicy": "same-actor-only",
        "artifactName": "windows-native-evidence-finalized-13000-1",
    }
    for head in evidence.HEADS:
        proof = json.loads(
            (output / f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json").read_text(encoding="utf-8")
        )
        assert proof["contractName"] == evidence.VISUAL_PROOF_CONTRACT
        assert proof["checks"] == {"capture_mode": "interactive", "human_review_confirmed": True}
        assert proof["readabilityReview"] == {"status": "passed", "reviewer": "accountable-reviewer"}
        assert [row["role"] for row in proof["screenshots"]] == ["progress", "completion"]
        assert proof["captureBinding"]["inventorySha256"] == digest(native / evidence.CAPTURE_INVENTORY_FILE)
        assert proof["finalizationBinding"] == finalization["finalizationSource"]


def test_capture_and_finalize_accept_exact_main_and_normal_tag_refs(tmp_path: Path) -> None:
    _, native, args = make_fixture(tmp_path)
    args.source_ref = "refs/heads/main"
    evidence.capture(args)

    finalize = finalize_args(native, tmp_path / "finalized")
    finalize.expected_ref = "refs/heads/main"
    finalize.finalization_ref = "refs/tags/v1.2.3"
    evidence.finalize(finalize)


def test_digest_parsers_accept_only_their_exact_documented_positive_shape() -> None:
    assert evidence.require_sha256(EXACT_SHA256, "bare digest") == EXACT_SHA256
    assert evidence.require_prefixed_sha256(
        f"sha256:{EXACT_SHA256}", "prefixed digest"
    ) == EXACT_SHA256


def test_capture_login_parser_accepts_only_the_exact_actions_bot_special_case() -> None:
    assert evidence.require_github_login(
        "github-actions[bot]", "capture actor"
    ) == "github-actions[bot]"
    assert evidence.require_github_login("normal-human", "capture actor") == "normal-human"
    for lookalike in (
        "github-actions[Bot]",
        "github-actions[bot]x",
        "github_actions[bot]",
        "human[bot]",
        "github-actions[]",
    ):
        with pytest.raises(evidence.ContractError, match="exact GitHub login"):
            evidence.require_github_login(lookalike, "capture actor")


@pytest.mark.parametrize(
    "value",
    [
        EXACT_SHA256.upper(),
        f"{EXACT_SHA256} ",
        f" {EXACT_SHA256}",
        f"sha256:{EXACT_SHA256}",
    ],
)
def test_bare_digest_parser_rejects_normalized_or_prefixed_shapes(value: str) -> None:
    with pytest.raises(evidence.ContractError, match="exact lowercase SHA-256"):
        evidence.require_sha256(value, "bare digest")


@pytest.mark.parametrize(
    "value",
    [
        f"SHA256:{EXACT_SHA256}",
        f"sha256:{EXACT_SHA256.upper()}",
        f"sha256:{EXACT_SHA256} ",
        EXACT_SHA256,
    ],
)
def test_prefixed_digest_parser_rejects_non_exact_shapes(value: str) -> None:
    with pytest.raises(evidence.ContractError, match="exact lowercase sha256:<hex>"):
        evidence.require_prefixed_sha256(value, "prefixed digest")


def test_materialize_authenticates_zip_and_creates_only_the_exact_private_held_snapshot(
    tmp_path: Path,
) -> None:
    candidate, _, archive, args = make_archive_fixture(tmp_path)

    materialized = evidence.materialize_candidate_archive(args)

    assert archive.is_file()
    assert materialized["root"] == args.held_root
    assert evidence.exact_candidate_tree(args.held_root) == args.held_root
    for relative in evidence.CANDIDATE_EXPORT_PATHS:
        assert (args.held_root / relative).read_bytes() == (candidate / relative).read_bytes()
    evidence.revalidate_candidate_snapshot(materialized)


def test_v2_materialize_creates_exact_publication_and_signing_parents(
    tmp_path: Path,
) -> None:
    candidate, _, args = make_fixture(tmp_path)
    upgrade_fixture_to_windows_only_scope(tmp_path, candidate, args)
    archive = tmp_path / "candidate-v2.zip"
    write_candidate_zip(archive, candidate)
    bind_archive_digest(args, archive)
    args.candidate_zip = archive
    args.held_root = tmp_path / "candidate-v2-held"

    materialized = evidence.materialize_candidate_archive(args)

    assert materialized["root"] == args.held_root
    for relative in (
        evidence.PUBLICATION_SCOPE.PUBLICATION_DIRECTORY,
        f"{evidence.PUBLICATION_SCOPE.PUBLICATION_DIRECTORY}/files",
        "signing",
    ):
        directory = args.held_root / relative
        assert directory.is_dir()
        assert not directory.is_symlink()
    assert list(
        (args.held_root / evidence.PUBLICATION_SCOPE.PUBLICATION_DIRECTORY / "files").iterdir()
    ) == []
    for relative in evidence.WINDOWS_ONLY_CANDIDATE_EXPORT_PATHS:
        assert (args.held_root / relative).read_bytes() == (candidate / relative).read_bytes()
    evidence.revalidate_candidate_snapshot(materialized)


def test_materialize_emits_exact_ten_file_live_lock_authority(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    _, _, _, args = make_archive_fixture(tmp_path)
    args.authority_json = tmp_path / "held-authority.json"

    evidence.materialize(args)

    authority = json.loads(args.authority_json.read_text(encoding="utf-8"))
    if os.name != "nt":
        assert stat.S_IMODE(args.authority_json.stat().st_mode) == 0o600
    assert set(authority) == {
        "artifactSha256",
        "contractName",
        "contractVersion",
        "files",
    }
    assert authority["contractName"] == evidence.HELD_SNAPSHOT_CONTRACT
    assert authority["contractVersion"] == 1
    assert authority["artifactSha256"] == json.loads(args.candidate_handoff_json)[
        "artifactSha256"
    ]
    assert authority["files"] == [
        {
            "path": relative,
            "sha256": digest(args.held_root / relative),
            "sizeBytes": (args.held_root / relative).stat().st_size,
        }
        for relative in sorted(evidence.CANDIDATE_EXPORT_PATHS)
    ]
    assert len(
        [line for line in capsys.readouterr().out.splitlines() if "=" in line]
    ) == 10


def test_materialize_preserves_preexisting_authority_and_cleans_new_held_root(
    tmp_path: Path,
) -> None:
    _, _, _, args = make_archive_fixture(tmp_path)
    args.authority_json = tmp_path / "held-authority.json"
    args.authority_json.write_text("operator-owned\n", encoding="utf-8")

    with pytest.raises(evidence.ContractError, match="must be an absolute path"):
        evidence.materialize(args)

    assert args.authority_json.read_text(encoding="utf-8") == "operator-owned\n"
    assert not args.held_root.exists()


def test_materialize_rejects_corrupted_transport_digest_before_zip_parsing(
    tmp_path: Path,
) -> None:
    _, _, archive, args = make_archive_fixture(tmp_path)
    with archive.open("ab") as handle:
        handle.write(b"corrupt-after-authentication")

    with pytest.raises(evidence.ContractError, match="REST digest"):
        evidence.materialize_candidate_archive(args)
    assert not args.held_root.exists()


@pytest.mark.parametrize(
    "attack", ["duplicate", "traversal", "symlink", "special", "zip-bomb"]
)
def test_materialize_rejects_unsafe_or_ambiguous_zip_members(
    tmp_path: Path, attack: str
) -> None:
    candidate, _, archive, args = make_archive_fixture(tmp_path)
    archive.unlink()
    first = evidence.CANDIDATE_EXPORT_PATHS[0]
    kwargs: dict[str, object] = {}
    if attack == "duplicate":
        kwargs["extra_members"] = [
            (first, (candidate / first).read_bytes(), stat.S_IFREG | 0o600)
        ]
    elif attack == "traversal":
        kwargs["renames"] = {first: "../RELEASE_CHANNEL.generated.json"}
    elif attack in {"symlink", "special"}:
        kwargs["replacements"] = {
            first: (
                b"files/chummer-avalonia-win-x64-installer.exe",
                (stat.S_IFLNK if attack == "symlink" else stat.S_IFIFO) | 0o777,
            )
        }
    else:
        kwargs["compression"] = zipfile.ZIP_DEFLATED
        kwargs["replacements"] = {
            evidence.candidate_payload_path("avalonia"): (
                b"0" * (4 * 1024 * 1024),
                stat.S_IFREG | 0o600,
            )
        }
    if attack == "duplicate":
        with pytest.warns(UserWarning, match="Duplicate name"):
            write_candidate_zip(archive, candidate, **kwargs)
    else:
        write_candidate_zip(archive, candidate, **kwargs)
    bind_archive_digest(args, archive)

    with pytest.raises(evidence.ContractError, match="seven|member|compression"):
        evidence.materialize_candidate_archive(args)
    assert not args.held_root.exists()
    assert not (tmp_path / "RELEASE_CHANNEL.generated.json").exists()


def test_materialize_rejects_post_extraction_same_size_mutation(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _, _, _, args = make_archive_fixture(tmp_path)
    original = evidence.validate_candidate_export

    def mutate_after_validation(namespace: argparse.Namespace) -> dict[str, object]:
        candidate = original(namespace)
        installer = namespace.candidate_root / evidence.candidate_installer_path("avalonia")
        installer.write_bytes(b"X" * installer.stat().st_size)
        return candidate

    monkeypatch.setattr(evidence, "validate_candidate_export", mutate_after_validation)
    with pytest.raises(evidence.ContractError, match="held candidate changed"):
        evidence.materialize_candidate_archive(args)
    assert not args.held_root.exists()


@pytest.mark.parametrize("attack", ["same-size", "symlink"])
def test_candidate_validation_rejects_descriptor_snapshot_swap_races(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, attack: str
) -> None:
    candidate, _, args = make_fixture(tmp_path)
    target_relative = evidence.candidate_installer_path("avalonia")
    target = candidate / target_relative
    original = evidence.snapshot_regular_beneath
    attacked = False

    def snapshot_then_swap(
        root: Path, relative: str, label: str, *, include_data: bool = False
    ) -> object:
        nonlocal attacked
        snapshot = original(root, relative, label, include_data=include_data)
        if root == candidate and relative == target_relative and not attacked:
            attacked = True
            if attack == "same-size":
                target.write_bytes(b"X" * snapshot.size_bytes)
            else:
                target.unlink()
                target.symlink_to(candidate / evidence.candidate_installer_path("blazor-desktop"))
        return snapshot

    monkeypatch.setattr(evidence, "snapshot_regular_beneath", snapshot_then_swap)
    with pytest.raises(evidence.ContractError, match="changed|symlink"):
        evidence.validate_candidate_export(args)


def test_provenance_copy_rejects_source_mutation_after_descriptor_copy(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate, native, args = make_fixture(tmp_path)
    authority = evidence.validate_candidate_export(args)
    original = evidence.copy_validated_held_member
    attacked = False

    def copy_then_mutate(
        candidate_authority: dict[str, object], source_name: str, target: Path, label: str
    ) -> object:
        nonlocal attacked
        copied = original(candidate_authority, source_name, target, label)
        if not attacked:
            attacked = True
            source = candidate / source_name
            data = bytearray(source.read_bytes())
            data[0] ^= 1
            source.write_bytes(data)
        return copied

    monkeypatch.setattr(evidence, "copy_validated_held_member", copy_then_mutate)
    with pytest.raises(evidence.ContractError, match="held candidate changed"):
        evidence.copy_candidate_provenance(authority, native)
    assert not (native / evidence.CANDIDATE_PROVENANCE_DIRECTORY).exists()


@pytest.mark.parametrize(
    "attack",
    [
        "binding-size",
        "inventory-contract",
        "inventory-row",
        "receipt-contract",
        "supply-category",
    ],
)
def test_finalize_explicitly_revalidates_preserved_candidate_provenance_contract(
    tmp_path: Path, attack: str
) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.capture(args)
    capture_path = native / evidence.CAPTURE_FILE
    capture_payload = json.loads(capture_path.read_text(encoding="utf-8"))
    if attack == "binding-size":
        capture_payload["candidate"]["contentInventory"]["sizeBytes"] += 1
    else:
        key = (
            "contentInventory"
            if attack in {"inventory-contract", "inventory-row"}
            else "exportReceipt"
        )
        filename = (
            evidence.CANDIDATE_INVENTORY_FILE
            if key == "contentInventory"
            else evidence.CANDIDATE_EXPORT_FILE
        )
        document_path = native / evidence.CANDIDATE_PROVENANCE_DIRECTORY / filename
        document = json.loads(document_path.read_text(encoding="utf-8"))
        if attack == "inventory-row":
            row = next(
                item
                for item in document["files"]
                if item["path"] == evidence.candidate_installer_path("avalonia")
            )
            row["sha256"] = "f" * 64
        elif attack == "supply-category":
            supply_chain = document["supplyChain"]
            supply_chain["sboms"][0], supply_chain["scans"][0] = (
                supply_chain["scans"][0],
                supply_chain["sboms"][0],
            )
            captured_supply_chain = capture_payload["candidate"]["supplyChain"]
            captured_supply_chain["sboms"][0], captured_supply_chain["scans"][0] = (
                captured_supply_chain["scans"][0],
                captured_supply_chain["sboms"][0],
            )
        else:
            document["contractName"] = "attacker.contract"
        write_json(document_path, document)
        new_sha = digest(document_path)
        capture_payload["candidate"][key]["sha256"] = new_sha
        capture_payload["candidate"][f"{key}Sha256"] = new_sha
        if attack == "inventory-row":
            receipt_path = (
                native
                / evidence.CANDIDATE_PROVENANCE_DIRECTORY
                / evidence.CANDIDATE_EXPORT_FILE
            )
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            receipt["contentInventory"]["sha256"] = new_sha
            write_json(receipt_path, receipt)
            receipt_sha = digest(receipt_path)
            capture_payload["candidate"]["exportReceipt"]["sha256"] = receipt_sha
            capture_payload["candidate"]["exportReceiptSha256"] = receipt_sha
    write_json(capture_path, capture_payload)
    refresh_capture_inventory(native)
    finalize = finalize_args(native, tmp_path / "finalized")

    with pytest.raises(
        evidence.ContractError,
        match=(
            "path/hash/size|contract is invalid|differs from the captured byte binding|"
            "sboms binding is malformed|differs from the exact expected value"
        ),
    ):
        evidence.finalize(finalize)
    assert not finalize.output_root.exists()


def test_preflight_accepts_exact_candidate_bytes_without_writing_evidence(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.preflight(args)

    output = capsys.readouterr().out
    manifest_sha = digest(args.candidate_root / evidence.CANDIDATE_MANIFEST_FILE)
    assert f"candidate_manifest_sha256={manifest_sha}" in output
    assert "candidate_content_inventory_sha256=" in output
    assert "candidate_export_receipt_sha256=" in output
    assert "avalonia_installer=files/chummer-avalonia-win-x64-installer.exe" in output
    assert not (native / evidence.CAPTURE_FILE).exists()
    assert not (native / evidence.CAPTURE_INVENTORY_FILE).exists()


@pytest.mark.parametrize("mutation", ["extra", "missing", "symlink"])
def test_preflight_rejects_non_exact_ten_file_export_tree(
    tmp_path: Path, mutation: str
) -> None:
    candidate, _, args = make_fixture(tmp_path)
    if mutation == "extra":
        (candidate / "unexpected.txt").write_text("unexpected\n", encoding="utf-8")
    elif mutation == "missing":
        (candidate / evidence.CANDIDATE_EXPORT_FILE).unlink()
    else:
        target = candidate / evidence.CANDIDATE_EXPORT_FILE
        target.unlink()
        target.symlink_to(candidate / evidence.CANDIDATE_INVENTORY_FILE)
    with pytest.raises(evidence.ContractError, match="ten-file|symlink"):
        evidence.preflight(args)


@pytest.mark.parametrize("mutation", ["newline", "extra", "missing", "boolean-version"])
def test_preflight_rejects_noncanonical_or_structurally_drifting_handoff(
    tmp_path: Path, mutation: str
) -> None:
    _, _, args = make_fixture(tmp_path)
    if mutation == "newline":
        args.candidate_handoff_json += "\n"
    else:
        payload = json.loads(args.candidate_handoff_json)
        if mutation == "extra":
            payload["callerClaim"] = "untrusted"
        elif mutation == "missing":
            del payload["artifactId"]
        else:
            payload["contractVersion"] = True
        args.candidate_handoff_json = canonical_json(payload)
    with pytest.raises(evidence.ContractError, match="canonical|missing or extra|contract"):
        evidence.preflight(args)


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("artifactId", "778"),
        ("artifactSha256", "f" * 64),
        ("runAttempt", "2"),
        ("actor", "other-producer"),
    ],
)
def test_preflight_rejects_authenticated_api_that_differs_from_handoff(
    tmp_path: Path, field: str, value: str
) -> None:
    _, _, args = make_fixture(tmp_path)
    args.candidate_api_json = mutate_canonical(args.candidate_api_json, field, value)
    with pytest.raises(evidence.ContractError, match="differs from the canonical handoff|must be exactly"):
        evidence.preflight(args)


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("artifactCreatedAt", "2999-01-01T00:00:00Z"),
        ("artifactExpiresAt", "2020-01-01T00:00:00Z"),
        ("artifactExpiresAt", "2999-01-01T00:00:00.000Z"),
        ("event", "push"),
        ("status", "in_progress"),
        ("conclusion", "failure"),
    ],
)
def test_preflight_rejects_expired_or_non_successful_api_authority(
    tmp_path: Path, field: str, value: str
) -> None:
    _, _, args = make_fixture(tmp_path)
    args.candidate_api_json = mutate_canonical(args.candidate_api_json, field, value)
    with pytest.raises(evidence.ContractError):
        evidence.preflight(args)


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("ref", "refs/heads/feature"),
        ("sha", "b" * 40),
        ("actor", "different-producer"),
        ("runAttempt", "2"),
        ("artifactName", "caller-selected-artifact"),
    ],
)
def test_preflight_rejects_export_receipt_source_drift(
    tmp_path: Path, field: str, value: str
) -> None:
    candidate, _, args = make_fixture(tmp_path)
    receipt_path = candidate / evidence.CANDIDATE_EXPORT_FILE
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["source"][field] = value
    write_json(receipt_path, receipt)
    with pytest.raises(evidence.ContractError, match="receipt source|runnerLabel"):
        evidence.preflight(args)


def test_capture_requires_exact_relay_actor_and_producer_commit(tmp_path: Path) -> None:
    _, _, args = make_fixture(tmp_path)
    args.source_actor = "capture-operator"
    with pytest.raises(evidence.ContractError, match="hosted producer relay"):
        evidence.capture(args)

    _, _, args = make_fixture(tmp_path / "sha")
    args.source_sha = "b" * 40
    with pytest.raises(evidence.ContractError, match="exact producer main commit"):
        evidence.capture(args)


@pytest.mark.parametrize("shape", ["uppercase", "padded", "prefixed"])
def test_preflight_rejects_non_exact_dispatched_digest_shapes(tmp_path: Path, shape: str) -> None:
    _, _, args = make_fixture(tmp_path)
    handoff = json.loads(args.candidate_handoff_json)
    args.candidate_handoff_json = mutate_canonical(
        args.candidate_handoff_json,
        "contentInventorySha256",
        malformed_digest(handoff["contentInventorySha256"], shape),
    )
    with pytest.raises(evidence.ContractError, match="exact lowercase SHA-256"):
        evidence.preflight(args)


@pytest.mark.parametrize("field", ["sha256", "payloadSha256"])
def test_preflight_rejects_non_exact_manifest_digest_fields(tmp_path: Path, field: str) -> None:
    candidate, _, args = make_fixture(tmp_path)
    manifest_path = candidate / evidence.CANDIDATE_MANIFEST_FILE
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["artifacts"][0][field] = "A" * 64
    write_json(manifest_path, manifest)
    with pytest.raises(evidence.ContractError):
        evidence.preflight(args)


@pytest.mark.parametrize("target", ["installer", "payload", "manifest"])
def test_preflight_rejects_tampered_candidate_bytes(tmp_path: Path, target: str) -> None:
    candidate, _, args = make_fixture(tmp_path)
    if target == "manifest":
        (candidate / evidence.CANDIDATE_MANIFEST_FILE).write_text("{}\n", encoding="utf-8")
    else:
        relative = (
            evidence.candidate_installer_path("avalonia")
            if target == "installer"
            else evidence.candidate_payload_path("avalonia")
        )
        (candidate / relative).write_bytes(b"tampered")
    with pytest.raises(evidence.ContractError, match="does not match|differ"):
        evidence.preflight(args)


@pytest.mark.parametrize("target", ["installer", "payload", "manifest"])
def test_capture_rejects_tampered_candidate_bytes(tmp_path: Path, target: str) -> None:
    candidate, _, args = make_fixture(tmp_path)
    if target == "manifest":
        (candidate / evidence.CANDIDATE_MANIFEST_FILE).write_text("{}\n", encoding="utf-8")
    else:
        relative = (
            evidence.candidate_installer_path("avalonia")
            if target == "installer"
            else evidence.candidate_payload_path("avalonia")
        )
        (candidate / relative).write_bytes(b"tampered")
    with pytest.raises(evidence.ContractError, match="does not match|differ"):
        evidence.capture(args)


def test_capture_rejects_different_triggering_actor(tmp_path: Path) -> None:
    _, _, args = make_fixture(tmp_path)
    args.source_triggering_actor = "human-operator"

    with pytest.raises(evidence.ContractError, match="same-actor"):
        evidence.capture(args)


@pytest.mark.parametrize("shape", ["uppercase", "padded", "prefixed"])
def test_capture_rejects_non_exact_dispatched_digest_shapes(tmp_path: Path, shape: str) -> None:
    _, _, args = make_fixture(tmp_path)
    handoff = json.loads(args.candidate_handoff_json)
    args.candidate_handoff_json = mutate_canonical(
        args.candidate_handoff_json,
        "contentInventorySha256",
        malformed_digest(handoff["contentInventorySha256"], shape),
    )
    with pytest.raises(evidence.ContractError, match="exact lowercase SHA-256"):
        evidence.capture(args)


@pytest.mark.parametrize(
    ("field", "value_shape"),
    [
        ("artifactDigest", "uppercase-prefix"),
        ("artifactDigest", "padded"),
        ("artifactDigest", "bare"),
        ("bootstrapPayloadSha256", "uppercase"),
        ("bootstrapPayloadSha256", "padded"),
        ("bootstrapPayloadSha256", "prefixed"),
    ],
)
def test_capture_rejects_non_exact_receipt_digest_shapes(
    tmp_path: Path, field: str, value_shape: str
) -> None:
    _, native, args = make_fixture(tmp_path)
    receipt_path = native / evidence.head_paths("avalonia")["receipt"]
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    value = receipt[field]
    if value_shape == "uppercase-prefix":
        value = value.replace("sha256:", "SHA256:")
    elif value_shape == "padded":
        value = f"{value} "
    elif value_shape == "bare":
        value = value.removeprefix("sha256:")
    else:
        value = malformed_digest(value, value_shape)
    receipt[field] = value
    write_json(receipt_path, receipt)
    with pytest.raises(evidence.ContractError, match="exact lowercase"):
        evidence.capture(args)


def test_capture_rejects_json_boolean_size_for_one_byte_payload(tmp_path: Path) -> None:
    _, native, args = make_fixture(tmp_path, payload_bytes=b"x")
    receipt_path = native / evidence.head_paths("avalonia")["receipt"]
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    assert receipt["bootstrapPayloadSizeBytes"] == 1
    receipt["bootstrapPayloadSizeBytes"] = True
    write_json(receipt_path, receipt)

    with pytest.raises(evidence.ContractError, match="positive byte count"):
        evidence.capture(args)


def test_preflight_rejects_json_boolean_export_binding_for_one_byte_payload(
    tmp_path: Path,
) -> None:
    candidate, _, args = make_fixture(tmp_path, payload_bytes=b"x")
    receipt_path = candidate / evidence.CANDIDATE_EXPORT_FILE
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    assert receipt["heads"][0]["payload"]["sizeBytes"] == 1
    receipt["heads"][0]["payload"]["sizeBytes"] = True
    write_json(receipt_path, receipt)

    with pytest.raises(evidence.ContractError, match="JSON type mismatch"):
        evidence.preflight(args)


def test_finalize_rejects_json_boolean_capture_binding_for_one_byte_payload(
    tmp_path: Path,
) -> None:
    _, native, args = make_fixture(tmp_path, payload_bytes=b"x")
    evidence.capture(args)
    capture_path = native / evidence.CAPTURE_FILE
    capture = json.loads(capture_path.read_text(encoding="utf-8"))
    assert capture["heads"][0]["payload"]["sizeBytes"] == 1
    capture["heads"][0]["payload"]["sizeBytes"] = True
    write_json(capture_path, capture)
    refresh_capture_inventory(native)
    finalize = finalize_args(native, tmp_path / "finalized")

    with pytest.raises(evidence.ContractError, match="positive byte count"):
        evidence.finalize(finalize)


def test_capture_rejects_digest_identical_or_malformed_screenshots(tmp_path: Path) -> None:
    _, native, args = make_fixture(tmp_path)
    paths = evidence.head_paths("avalonia")
    (native / paths["completionScreenshot"]).write_bytes((native / paths["progressScreenshot"]).read_bytes())
    with pytest.raises(evidence.ContractError, match="digest-identical"):
        evidence.capture(args)

    _, native, args = make_fixture(tmp_path / "malformed")
    png = native / evidence.head_paths("avalonia")["progressScreenshot"]
    png.write_bytes(png.read_bytes()[:-5])
    with pytest.raises(evidence.ContractError, match="PNG|chunk|IEND"):
        evidence.capture(args)


def test_capture_rejects_non_native_or_incomplete_progress(tmp_path: Path) -> None:
    _, native, args = make_fixture(tmp_path)
    receipt_path = native / evidence.head_paths("avalonia")["receipt"]
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["executionEnvironment"] = "wine_compatibility"
    write_json(receipt_path, receipt)
    with pytest.raises(evidence.ContractError, match="not native Windows"):
        evidence.capture(args)

    _, native, args = make_fixture(tmp_path / "progress")
    (native / evidence.head_paths("avalonia")["progressLog"]).write_text("Install complete\n", encoding="utf-8")
    with pytest.raises(evidence.ContractError, match="missing marker"):
        evidence.capture(args)


def test_capture_rejects_bare_or_ambiguous_source_refs(tmp_path: Path) -> None:
    _, _, args = make_fixture(tmp_path)
    args.source_ref = "candidate"
    with pytest.raises(evidence.ContractError, match="exact full refs/heads"):
        evidence.capture(args)


@pytest.mark.parametrize(
    ("field", "value", "message"),
    [
        ("source_sha", CAPTURE_SHA.upper(), "commit SHA"),
        ("source_ref", " refs/heads/main", "exact full refs/heads"),
        ("source_ref", "refs/heads/foo.lock/bar", "exact full refs/heads"),
    ],
)
def test_capture_rejects_non_exact_commits_and_git_invalid_refs(
    tmp_path: Path, field: str, value: str, message: str
) -> None:
    _, _, args = make_fixture(tmp_path)
    setattr(args, field, value)
    with pytest.raises(evidence.ContractError, match=message):
        evidence.capture(args)


@pytest.mark.parametrize(
    ("field", "value", "message"),
    [
        ("sha", f"{CANDIDATE_SHA} ", "commit SHA"),
        ("ref", "refs/heads/.hidden", "must be exactly"),
    ],
)
def test_capture_rejects_non_exact_producer_authority(
    tmp_path: Path, field: str, value: str, message: str
) -> None:
    _, _, args = make_fixture(tmp_path)
    args.candidate_handoff_json = mutate_canonical(args.candidate_handoff_json, field, value)
    args.candidate_api_json = mutate_canonical(args.candidate_api_json, field, value)
    with pytest.raises(evidence.ContractError, match=message):
        evidence.capture(args)


def test_finalize_rejects_inventory_tampering(tmp_path: Path) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.capture(args)
    finalize = finalize_args(native, tmp_path / "finalized")
    (native / evidence.head_paths("avalonia")["progressLog"]).write_text("tampered\n", encoding="utf-8")
    with pytest.raises(evidence.ContractError, match="inventory"):
        evidence.finalize(finalize)


def test_capture_inventory_rejects_boolean_size_for_one_byte_file(tmp_path: Path) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.capture(args)
    target_relative = evidence.head_paths("avalonia")["progressLog"]
    (native / target_relative).write_bytes(b"x")
    inventory_path = native / evidence.CAPTURE_INVENTORY_FILE
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    inventory["files"] = evidence.exact_inventory(
        native, exclude={evidence.CAPTURE_INVENTORY_FILE}
    )
    target_row = next(row for row in inventory["files"] if row["path"] == target_relative)
    assert target_row["sizeBytes"] == 1
    target_row["sizeBytes"] = True
    write_json(inventory_path, inventory)

    with pytest.raises(evidence.ContractError, match="JSON type mismatch"):
        evidence.verify_inventory(native, digest(inventory_path))


@pytest.mark.parametrize("target", ["inventory", "capture"])
def test_finalize_rejects_boolean_contract_versions(
    tmp_path: Path, target: str
) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.capture(args)
    if target == "inventory":
        path = native / evidence.CAPTURE_INVENTORY_FILE
        payload = json.loads(path.read_text(encoding="utf-8"))
        payload["contractVersion"] = True
        write_json(path, payload)
    else:
        path = native / evidence.CAPTURE_FILE
        payload = json.loads(path.read_text(encoding="utf-8"))
        payload["contractVersion"] = True
        write_json(path, payload)
        refresh_capture_inventory(native)
    finalize = finalize_args(native, tmp_path / f"finalized-{target}")

    with pytest.raises(evidence.ContractError, match=f"{target}.*contract is invalid"):
        evidence.finalize(finalize)


@pytest.mark.parametrize("shape", ["uppercase", "padded", "prefixed"])
def test_finalize_rejects_non_exact_inventory_digest_input(tmp_path: Path, shape: str) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.capture(args)
    finalize = finalize_args(native, tmp_path / f"finalized-{shape}")
    finalize.capture_inventory_sha256 = malformed_digest(finalize.capture_inventory_sha256, shape)
    with pytest.raises(evidence.ContractError, match="exact lowercase SHA-256"):
        evidence.finalize(finalize)


def test_finalize_rejects_non_exact_capture_manifest_digest_field(tmp_path: Path) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.capture(args)
    inventory_path = native / evidence.CAPTURE_INVENTORY_FILE
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    inventory["captureManifestSha256"] = "A" * 64
    write_json(inventory_path, inventory)
    finalize = finalize_args(native, tmp_path / "finalized-inventory-digest")
    with pytest.raises(evidence.ContractError, match="exact lowercase SHA-256"):
        evidence.finalize(finalize)


@pytest.mark.parametrize(
    "mutation",
    [
        "self",
        "not-allowlisted",
        "unconfirmed",
        "source-sha",
        "finalizer-actor",
        "finalizer-triggering-actor",
        "finalizer-sha",
    ],
)
def test_finalize_rejects_unaccountable_or_unbound_review(tmp_path: Path, mutation: str) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.capture(args)
    finalize = finalize_args(native, tmp_path / f"finalized-{mutation}")
    if mutation == "self":
        finalize.reviewer_id = "capture-operator"
        finalize.reviewer_allowlist_json = '["capture-operator"]'
    elif mutation == "not-allowlisted":
        finalize.reviewer_allowlist_json = '["backup-reviewer"]'
    elif mutation == "unconfirmed":
        finalize.avalonia_contrast = "false"
    else:
        if mutation == "source-sha":
            finalize.expected_sha = "c" * 40
        elif mutation == "finalizer-actor":
            finalize.finalization_actor = "different-reviewer"
        elif mutation == "finalizer-triggering-actor":
            finalize.finalization_triggering_actor = "different-reviewer"
        else:
            finalize.finalization_sha = "c" * 40
    with pytest.raises(evidence.ContractError):
        evidence.finalize(finalize)


@pytest.mark.parametrize("field", ["expected_ref", "finalization_ref"])
def test_finalize_rejects_bare_or_ambiguous_source_refs(tmp_path: Path, field: str) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.capture(args)
    finalize = finalize_args(native, tmp_path / f"finalized-{field}")
    setattr(finalize, field, "codex/native-evidence")
    with pytest.raises(evidence.ContractError, match="exact full refs/heads"):
        evidence.finalize(finalize)


@pytest.mark.parametrize(
    ("field", "value", "message"),
    [
        ("expected_sha", CAPTURE_SHA.upper(), "commit SHA"),
        ("finalization_sha", f"{CAPTURE_SHA} ", "commit SHA"),
        ("expected_ref", "refs/heads/main ", "exact full refs/heads"),
        ("finalization_ref", "refs/tags/.hidden", "exact full refs/heads"),
        ("expected_ref", "refs/heads/foo.lock/bar", "exact full refs/heads"),
    ],
)
def test_finalize_rejects_non_exact_commits_and_git_invalid_refs(
    tmp_path: Path, field: str, value: str, message: str
) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.capture(args)
    finalize = finalize_args(native, tmp_path / f"finalized-{field}")
    setattr(finalize, field, value)
    with pytest.raises(evidence.ContractError, match=message):
        evidence.finalize(finalize)


def workflow_path_match_results(cases: list[str], source: dict[str, str]) -> list[bool]:
    helper = REPO_ROOT / "scripts/github_workflow_run_path.js"
    bare = ".github/workflows/windows-native-evidence-capture.yml"
    script = """
    const { workflowRunPathMatches } = require(process.argv[1]);
    const cases = JSON.parse(process.argv[2]);
    const source = JSON.parse(process.argv[3]);
    process.stdout.write(JSON.stringify(cases.map(row =>
      workflowRunPathMatches(row.actual, row.bare, source))));
    """
    completed = subprocess.run(
        [
            "node",
            "-e",
            script,
            str(helper),
            json.dumps([{"actual": actual, "bare": bare} for actual in cases]),
            json.dumps(source),
        ],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    return json.loads(completed.stdout)


@pytest.mark.parametrize(
    ("lane", "branch", "exact_ref", "opposite_ref"),
    [
        pytest.param(
            "capture", "main", "refs/heads/main", "refs/tags/main",
            id="capture-main-rejects-tags-for-heads",
        ),
        pytest.param(
            "finalize", "v1.2.3", "refs/tags/v1.2.3", "refs/heads/v1.2.3",
            id="finalize-rejects-heads-for-tags",
        ),
    ],
)
def test_workflow_run_path_matcher_accepts_only_exact_ref_kind(
    lane: str, branch: str, exact_ref: str, opposite_ref: str
) -> None:
    assert lane in {"capture", "finalize"}
    bare = ".github/workflows/windows-native-evidence-capture.yml"
    source = {"branch": branch, "ref": exact_ref, "sha": CAPTURE_SHA}
    cases = [
        bare,
        f"{bare}@{branch}",
        f"{bare}@{exact_ref}",
        f"{bare}@{CAPTURE_SHA}",
        f"{bare}@{opposite_ref}",
        f"{bare}@refs/heads/other",
        f"{bare}@{branch}-other",
        f"{bare}@{CAPTURE_SHA}0",
        f"{bare}-other@{branch}",
        f"{bare}@{branch}/other",
    ]
    assert workflow_path_match_results(cases, source) == [
        True, True, True, True, False, False, False, False, False, False
    ]


def test_workflow_run_path_matcher_rejects_bare_claimed_source_ref() -> None:
    bare = ".github/workflows/windows-native-evidence-capture.yml"
    source = {"branch": "main", "ref": "main", "sha": CAPTURE_SHA}
    assert workflow_path_match_results([bare, f"{bare}@main", f"{bare}@{CAPTURE_SHA}"], source) == [
        False, False, False
    ]


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("branch", "main "),
        ("ref", " refs/heads/main"),
        ("sha", CAPTURE_SHA.upper()),
        ("sha", f"{CAPTURE_SHA} "),
    ],
)
def test_workflow_run_path_matcher_does_not_canonicalize_source_values(
    field: str, value: str
) -> None:
    bare = ".github/workflows/windows-native-evidence-capture.yml"
    source = {"branch": "main", "ref": "refs/heads/main", "sha": CAPTURE_SHA}
    source[field] = value
    assert workflow_path_match_results([bare, f"{bare}@main", f"{bare}@{CAPTURE_SHA}"], source) == [
        False, False, False
    ]


def test_finalization_workflow_passes_github_ref_through_step_environment() -> None:
    workflow_path = REPO_ROOT / ".github/workflows/windows-native-evidence-finalize.yml"
    workflow_text = workflow_path.read_text(encoding="utf-8")
    workflow = yaml.load(workflow_text, Loader=yaml.BaseLoader)
    steps = workflow["jobs"]["finalize"]["steps"]
    finalization_step = next(
        step
        for step in steps
        if step["name"] == "Revalidate capture and emit independently reviewed proofs"
    )

    assert all("${{ github.ref }}" not in step.get("run", "") for step in steps)
    assert finalization_step["env"]["FINALIZATION_REF"] == "${{ github.ref }}"
    assert '--finalization-ref "$FINALIZATION_REF"' in finalization_step["run"]
    assert '--finalization-ref "${{ github.ref }}"' not in workflow_text


def test_workflows_are_read_only_artifact_lanes_with_allowlisted_human_review_of_bot_capture() -> None:
    capture_path = REPO_ROOT / ".github/workflows/windows-native-evidence-capture.yml"
    finalize_path = REPO_ROOT / ".github/workflows/windows-native-evidence-finalize.yml"
    capture = capture_path.read_text(encoding="utf-8")
    finalize = finalize_path.read_text(encoding="utf-8")
    combined = (capture + finalize).lower()
    assert "runs-on: windows-latest" in capture
    assert "actions/upload-artifact@" in capture
    assert "actions/download-artifact@" not in capture
    assert "github.rest.actions.downloadArtifact" in capture
    assert "archiveBytes.byteLength !== artifact.size_in_bytes" in capture
    assert "crypto.createHash('sha256').update(archiveBytes).digest('hex')" in capture
    assert "`sha256:${archiveDigest}` !== artifact.digest" in capture
    assert "core.setOutput('candidate_held_root', path.join(privateRoot, 'held'))" in capture
    assert "!path.isAbsolute(process.env.RUNNER_TEMP)" in capture
    assert "environment: windows-visual-review" in finalize
    assert "${{ github.actor }}" in finalize
    assert "${{ vars.windows_visual_reviewer_allowlist }}" in finalize.lower()
    assert "id: upload-capture" in capture and "artifact-digest" in capture
    assert "id: upload-finalized" in finalize and "artifact-digest" in finalize
    assert "--finalization-workflow .github/workflows/windows-native-evidence-finalize.yml" in finalize
    assert capture.count("require('./scripts/github_workflow_run_path.js')") == 1
    assert capture.count("require('./scripts/github_wait_for_workflow_run.js')") == 1
    assert "waitForExactSuccessfulWorkflowRun" in capture
    assert finalize.count("require('./scripts/github_workflow_run_path.js')") == 1
    assert "candidate_handoff_json must use exact canonical JSON serialization" in capture
    assert "native capture must be dispatched by the hosted producer relay" in capture
    assert "artifact.expired !== false" in capture
    assert "artifact.digest !== `sha256:${handoff.artifactSha256}`" in capture
    assert "artifact.size_in_bytes > 2 * 1024 * 1024 * 1024" in capture
    assert "expiresAt <= now" in capture
    assert "createdAt > now + 5 * 60 * 1000" in capture
    assert "capture_ref must be an exact full refs/heads/... or refs/tags/... source ref" in finalize
    assert capture.count("run.data.event !== 'workflow_dispatch'") == 1
    assert finalize.count("run.data.event !== 'workflow_dispatch'") == 1
    for path in (capture_path, finalize_path):
        workflow = yaml.load(path.read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
        assert len(workflow["on"]["workflow_dispatch"]["inputs"]) <= 10
        for job in workflow["jobs"].values():
            for step in job["steps"]:
                if "uses" in step:
                    assert re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}", step["uses"])
    capture_workflow = yaml.load(capture, Loader=yaml.BaseLoader)
    assert set(capture_workflow["on"]["workflow_dispatch"]["inputs"]) == {
        "candidate_handoff_json",
        "live_release_channel_json",
        "n_minus_one_release_json",
    }
    assert (
        "https://chummer.run/downloads/RELEASE_CHANNEL.generated.json"
        in capture
    )
    assert "validate-windows-relay-authority" in capture
    assert "live_release_channel_sha256" in capture
    assert "selected_tuple_sha256" in capture
    assert (
        capture_workflow["on"]["workflow_dispatch"]["inputs"][
            "n_minus_one_release_json"
        ]["required"]
        == "true"
    )
    assert "run-windows-native-lifecycle-e2e.ps1" in capture
    assert "lifecycle_receipt_sha256" in capture
    capture_steps = capture_workflow["jobs"]["capture"]["steps"]
    step_names = [step["name"] for step in capture_steps]
    checkout_index = step_names.index("Check out evidence contract")
    relay_authority_index = step_names.index(
        "Validate immutable N-1 and signer relay authority"
    )
    candidate_auth_index = step_names.index(
        "Authenticate exact candidate run and artifact"
    )
    assert checkout_index < relay_authority_index < candidate_auth_index
    relay_authority = capture_steps[relay_authority_index]
    assert "validate-windows-relay-authority" in relay_authority["run"]
    assert "CHUMMER_WINDOWS_AUTHENTICODE_SIGNER_CERT_SHA256" in json.dumps(
        relay_authority
    )
    assert "VALIDATED_N_MINUS_ONE_RELEASE_SHA256" in capture
    assert "authenticodeSignerCertificateSha256" in capture
    assert "authenticodeSignerSpkiSha256" in capture
    assert "contractVersion: 4" in capture
    preflight_index = step_names.index(
        "Authenticate ZIP and materialize one private held candidate before execution"
    )
    assert preflight_index == step_names.index("Authenticate exact candidate run and artifact") + 1
    locked_step_name = "Execute and capture only while exact held snapshot handles are locked"
    locked_index = step_names.index(locked_step_name)
    assert preflight_index < locked_index
    locked_step = capture_steps[locked_index]
    assert locked_step["env"]["CANDIDATE_ROOT"] == (
        "${{ steps.candidate-run.outputs.candidate_held_root }}"
    )
    locked_run = locked_step["run"]
    assert locked_step["env"]["AUTHENTICODE_SIGNER_CERTIFICATE_SHA256"] == (
        "${{ vars.CHUMMER_WINDOWS_AUTHENTICODE_SIGNER_CERT_SHA256 }}"
    )
    assert locked_step["env"]["AUTHENTICODE_SIGNER_SPKI_SHA256"] == (
        "${{ vars.CHUMMER_WINDOWS_AUTHENTICODE_SIGNER_SPKI_SHA256 }}"
    )
    assert locked_step["env"]["LIVE_RELEASE_CHANNEL_JSON"] == (
        "${{ inputs.live_release_channel_json }}"
    )
    assert locked_step["env"]["VALIDATED_N_MINUS_ONE_RELEASE_SHA256"] == (
        "${{ steps.relay-authority.outputs.n_minus_one_release_sha256 }}"
    )
    assert locked_step["env"]["VALIDATED_LIVE_RELEASE_CHANNEL_SHA256"] == (
        "${{ steps.relay-authority.outputs.live_release_channel_sha256 }}"
    )
    assert locked_step["env"]["VALIDATED_SELECTED_TUPLE_SHA256"] == (
        "${{ steps.relay-authority.outputs.selected_tuple_sha256 }}"
    )
    for lifecycle_authority_argument in (
        "-LiveReleaseChannelJson",
        "-ExpectedNMinusOneReleaseSha256",
        "-ExpectedLiveReleaseChannelSha256",
        "-ExpectedSelectedTupleSha256",
    ):
        assert lifecycle_authority_argument in locked_run
    assert "[IO.FileShare]::Read" in locked_run
    assert "Get-LiveHeldIdentity" in locked_run
    assert "Live held handle differs at lock acquisition" in locked_run
    assert "windows_native_evidence.py preflight" in locked_run
    assert "Under-lock candidate preflight failed with exit" in locked_run
    assert locked_run.count("verify-windows-authenticode.ps1") == 1
    assert "$authenticodeExit = $LASTEXITCODE" in locked_run
    assert "Independent Authenticode verification failed with exit" in locked_run
    assert "--expected-authenticode-signer-certificate-sha256" in locked_run
    assert "--expected-authenticode-signer-spki-sha256" in locked_run
    assert locked_run.count("run-desktop-startup-smoke.sh") == 1
    assert locked_run.count("capture_windows_installer_visual.ps1") == 1
    assert "$avaloniaStartupExit = $LASTEXITCODE" in locked_run
    assert "$avaloniaVisualExit = $LASTEXITCODE" in locked_run
    assert "$captureExit = $LASTEXITCODE" in locked_run
    assert "Native evidence capture failed with exit" in locked_run
    assert "Live held handle differs after capture" in locked_run
    assert locked_run.index("windows_native_evidence.py preflight") < locked_run.index(
        "run-desktop-startup-smoke.sh"
    )
    assert locked_run.index("verify-windows-authenticode.ps1") < locked_run.index(
        "run-desktop-startup-smoke.sh"
    )
    assert locked_run.rindex("Live held handle differs after capture") > locked_run.index(
        "windows_native_evidence.py capture"
    )
    assert "${{ github.workspace }}/candidate" not in capture
    preflight_run = capture_steps[preflight_index]["run"]
    assert "windows_native_evidence.py materialize" in preflight_run
    for binding in (
        "--candidate-zip",
        "--held-root",
        "--authority-json",
        "--candidate-handoff-json",
        "--candidate-api-json",
    ):
        assert binding in preflight_run
    for forbidden_binding in (
        "--candidate-manifest-sha256",
        "--avalonia-installer-sha256",
        "--release-identity-json",
    ):
        assert forbidden_binding not in preflight_run
    finalize_workflow = yaml.load(finalize, Loader=yaml.BaseLoader)
    finalize_step_names = [step["name"] for step in finalize_workflow["jobs"]["finalize"]["steps"]]
    assert finalize_step_names.index("Check out finalization contract") < finalize_step_names.index(
        "Authenticate exact capture run, actor, ref, and artifact"
    )
    assert "contents: read" in capture and "actions: read" in capture
    assert "contents: read" in finalize and "actions: read" in finalize

    verifier = (REPO_ROOT / "scripts" / "verify-windows-authenticode.ps1").read_text(
        encoding="utf-8"
    )
    for required in (
        "Get-AuthenticodeSignature -LiteralPath",
        "SignatureStatus]::Valid",
        "SignatureType.ToString() -cne 'Authenticode'",
        "X509RevocationMode]::Online",
        "X509RevocationFlag]::EntireChain",
        "X509VerificationFlags]::NoFlag",
        "1.3.6.1.5.5.7.3.3",
        "1.3.6.1.5.5.7.3.8",
        "1.2.840.113549.1.9.16.2.14",
        "2.16.840.1.101.3.4.2.1",
        "$AuthenticodeSigner.GetSignature()",
        "$timestampCms.CheckSignature($true)",
        "[IO.FileMode]::CreateNew",
    ):
        assert required in verifier
    assert "ExpectedSignerCertificateSha256" in verifier
    assert "ExpectedSignerSpkiSha256" in verifier
    for forbidden in (
        "secrets.", "contents: write", "actions: write", "packages: write", "id-token: write",
        "softprops/action-gh-release", "ncipollo/release-action", "gh release", "release create",
    ):
        assert forbidden not in combined

    docs = (REPO_ROOT / "docs/WINDOWS_NATIVE_EVIDENCE.md").read_text(encoding="utf-8")
    assert "original ZIP" in docs
    assert "CHUMMER_PREVIEW_NIGHTLY_NATIVE_WINDOWS_EVIDENCE_ARCHIVE" in docs
    assert "read-only GitHub Actions REST API" in docs
    assert "tree-digest substitute" in docs
    assert "unambiguous full source ref" not in docs
