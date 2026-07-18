from __future__ import annotations

import argparse
import binascii
import hashlib
import importlib.util
import json
import re
import struct
import subprocess
import sys
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
VERSION = "preview-20260718.1"
CHANNEL = "preview"
CAPTURE_SHA = "a" * 40
CANDIDATE_SHA = CAPTURE_SHA
EXACT_SHA256 = "d" * 64
ARTIFACT_SHA256 = "e" * 64


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


def make_fixture(root: Path) -> tuple[Path, Path, argparse.Namespace]:
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
        payload.write_bytes(b"PK" + bytes([index + 10]) * 2048)
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
            "artifacts": rows,
        },
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
        output_artifact_name="windows-native-evidence-12345-1",
    )
    return candidate, native, args


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
        finalization_artifact_name="windows-native-evidence-finalized-13000-1",
        avalonia_readability="true",
        avalonia_contrast="true",
        avalonia_clipping="true",
        blazor_desktop_readability="true",
        blazor_desktop_contrast="true",
        blazor_desktop_clipping="true",
    )


def test_capture_and_independent_finalize_emit_stage_compatible_proofs(tmp_path: Path) -> None:
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
def test_preflight_rejects_non_exact_seven_file_export_tree(
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
    with pytest.raises(evidence.ContractError, match="seven-file|symlink"):
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
    "mutation", ["self", "not-allowlisted", "unconfirmed", "source-sha", "finalizer-actor", "finalizer-sha"]
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
        finalize.blazor_desktop_contrast = "false"
    else:
        if mutation == "source-sha":
            finalize.expected_sha = "c" * 40
        elif mutation == "finalizer-actor":
            finalize.finalization_actor = "different-reviewer"
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


def test_workflows_are_read_only_artifact_lanes_with_independent_review() -> None:
    capture_path = REPO_ROOT / ".github/workflows/windows-native-evidence-capture.yml"
    finalize_path = REPO_ROOT / ".github/workflows/windows-native-evidence-finalize.yml"
    capture = capture_path.read_text(encoding="utf-8")
    finalize = finalize_path.read_text(encoding="utf-8")
    combined = (capture + finalize).lower()
    assert "runs-on: windows-latest" in capture
    assert "actions/upload-artifact@" in capture
    assert "actions/download-artifact@" in capture
    assert "environment: windows-visual-review" in finalize
    assert "${{ github.actor }}" in finalize
    assert "${{ vars.windows_visual_reviewer_allowlist }}" in finalize.lower()
    assert "id: upload-capture" in capture and "artifact-digest" in capture
    assert "id: upload-finalized" in finalize and "artifact-digest" in finalize
    assert "--finalization-workflow .github/workflows/windows-native-evidence-finalize.yml" in finalize
    assert capture.count("require('./scripts/github_workflow_run_path.js')") == 1
    assert finalize.count("require('./scripts/github_workflow_run_path.js')") == 1
    assert "candidate_handoff_json must use exact canonical JSON serialization" in capture
    assert "native capture must be dispatched by the hosted producer relay" in capture
    assert "artifact.expired !== false" in capture
    assert "artifact.digest !== `sha256:${handoff.artifactSha256}`" in capture
    assert "expiresAt <= Date.now()" in capture
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
        "candidate_handoff_json"
    }
    capture_steps = capture_workflow["jobs"]["capture"]["steps"]
    step_names = [step["name"] for step in capture_steps]
    assert step_names.index("Check out evidence contract") < step_names.index(
        "Authenticate exact candidate run and artifact"
    )
    download_index = step_names.index("Download only the authenticated candidate artifact ID")
    preflight_index = step_names.index("Preflight exact candidate bytes before execution")
    assert preflight_index == download_index + 1
    download = capture_steps[download_index]
    assert download["with"]["artifact-ids"] == "${{ steps.candidate-run.outputs.artifact_id }}"
    assert "name" not in download["with"]
    for executable_step in (
        "Native startup receipt - Avalonia",
        "Native startup receipt - Blazor Desktop",
        "Capture interactive Avalonia progress and completion",
        "Capture interactive Blazor Desktop progress and completion",
    ):
        assert preflight_index < step_names.index(executable_step)
    preflight_run = capture_steps[preflight_index]["run"]
    assert "windows_native_evidence.py preflight" in preflight_run
    for binding in ("--candidate-handoff-json", "--candidate-api-json"):
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
