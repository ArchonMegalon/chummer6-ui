from __future__ import annotations

import argparse
import base64
import binascii
import hashlib
import importlib.util
import json
import re
import struct
import sys
import zlib
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]
CAPTURE_SHA = "c" * 40


def load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


export_fixtures = load(
    "unsigned_candidate_export_fixture_for_native_evidence",
    ROOT / "tests" / "test_preview_nightly_unsigned_candidate_export.py",
)
evidence = load(
    "unsigned_windows_preview_native_evidence_for_tests",
    ROOT / "scripts" / "unsigned_windows_preview_native_evidence.py",
)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def png_chunk(kind: bytes, data: bytes) -> bytes:
    return (
        struct.pack(">I", len(data))
        + kind
        + data
        + struct.pack(
            ">I", binascii.crc32(kind + data) & 0xFFFFFFFF
        )
    )


def write_png(path: Path, rgb: tuple[int, int, int]) -> None:
    width, height = 320, 200
    scanline = b"\x00" + bytes(rgb) * width
    pixels = scanline * height
    payload = (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(
            b"IHDR",
            struct.pack(
                ">IIBBBBB", width, height, 8, 2, 0, 0, 0
            ),
        )
        + png_chunk(b"IDAT", zlib.compress(pixels))
        + png_chunk(b"IEND", b"")
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)


def source_binding(
    run_id: str = "20001", run_attempt: str = "1"
) -> dict[str, str]:
    return {
        "actor": "github-actions[bot]",
        "artifactName": (
            f"unsigned-windows-preview-native-evidence-"
            f"{run_id}-{run_attempt}"
        ),
        "ref": evidence.SOURCE_REF,
        "repository": evidence.SOURCE_REPOSITORY,
        "rerunPolicy": evidence.RERUN_POLICY,
        "runAttempt": run_attempt,
        "runId": run_id,
        "sha": CAPTURE_SHA,
        "triggeringActor": "github-actions[bot]",
        "workflow": evidence.CAPTURE_WORKFLOW,
    }


def write_native_inputs(
    root: Path,
    candidate: Path,
    version: str,
    source: dict[str, str],
) -> None:
    installer = candidate / evidence.EXPORT.INSTALLER_PATH
    payload = candidate / evidence.EXPORT.PAYLOAD_PATH
    installer_binding = {
        "fileName": installer.name,
        "sha256": digest(installer),
        "sizeBytes": installer.stat().st_size,
    }
    payload_binding = {
        "fileName": payload.name,
        "sha256": digest(payload),
        "sizeBytes": payload.stat().st_size,
    }
    paths = evidence.WINDOWS.head_paths(evidence.HEAD)
    write_json(
        root / paths["receipt"],
        {
            "artifactDigest": f"sha256:{digest(installer)}",
            "artifactFileName": installer.name,
            "bootstrapPayloadAcquisitionMode": "download",
            "bootstrapPayloadFileName": payload.name,
            "bootstrapPayloadSha256": digest(payload),
            "bootstrapPayloadSizeBytes": payload.stat().st_size,
            "channelId": "preview",
            "executionEnvironment": "native_windows",
            "headId": evidence.HEAD,
            "nativeHostEvidence": {
                "contractName": evidence.WINDOWS.NATIVE_HOST_CONTRACT,
                "evidenceSource": "GitHub-hosted windows-latest",
                "hostKernel": "MINGW64_NT-10.0",
                "hostPlatform": "windows",
                "isNativeWindows": True,
                "runner": "powershell.exe",
                "status": "verified",
            },
            "platform": "windows",
            "readyCheckpoint": "pre_ui_event_loop",
            "releaseVersion": version,
            "rid": evidence.RID,
            "status": "pass",
        },
    )
    progress = root / paths["progressLog"]
    progress.parent.mkdir(parents=True, exist_ok=True)
    progress.write_text(
        "\n".join(evidence.WINDOWS.PROGRESS_MARKERS) + "\n",
        encoding="utf-8",
    )
    write_png(root / paths["progressScreenshot"], (20, 40, 60))
    write_png(root / paths["completionScreenshot"], (80, 100, 120))
    write_png(root / evidence.STARTUP_SCREENSHOT, (140, 160, 180))
    startup = root / evidence.STARTUP_LOG
    startup.parent.mkdir(parents=True, exist_ok=True)
    startup.write_text("native startup passed\n", encoding="utf-8")
    payload_http = root / evidence.PAYLOAD_HTTP_LOG
    payload_http.write_text(
        "candidate payload download passed\n", encoding="utf-8"
    )
    write_json(
        root / evidence.AUTHENTICODE_FILE,
        {
            "artifact": {
                **installer_binding,
                "path": evidence.EXPORT.INSTALLER_PATH,
            },
            "contractName": evidence.AUTHENTICODE_CONTRACT,
            "contractVersion": 1,
            "generatedAt": "2026-07-26T10:00:00.0000000Z",
            "nativeHostEvidence": {
                "contractName": evidence.WINDOWS.NATIVE_HOST_CONTRACT,
                "evidenceSource": "GitHub-hosted windows-latest",
                "hostPlatform": "windows",
                "isNativeWindows": True,
                "runner": "pwsh",
                "status": "verified",
            },
            "signatureStatus": "unsigned",
            "signingRequired": False,
            "source": source,
            "status": "verified",
            "unsignedReason": "preview_policy",
            "verifier": {
                "authenticodeStatus": "NotSigned",
                "implementation": (
                    "scripts/"
                    "verify_unsigned_windows_preview_authenticode.ps1"
                ),
                "platform": "windows",
                "securityDirectoryEmpty": True,
            },
        },
    )
    write_json(
        root / evidence.STARTUP_VISUAL_RECEIPT,
        {
            "candidate": {
                "installer": {
                    **installer_binding,
                    "path": evidence.EXPORT.INSTALLER_PATH,
                },
                "payload": {
                    **payload_binding,
                    "path": evidence.EXPORT.PAYLOAD_PATH,
                },
                "release": {"channel": "preview", "version": version},
                "signature": {
                    "policy": "preview_policy",
                    "required": False,
                    "status": "unsigned",
                },
                "sourceSha": export_fixtures.scope_fixtures.SOURCE_SHA,
            },
            "contractName": evidence.STARTUP_VISUAL_CONTRACT,
            "contractVersion": 1,
            "generatedAtUtc": "2026-07-26T10:00:01Z",
            "installedExecutable": {
                "fileName": "Chummer.Avalonia.exe",
                "payloadEntry": "Chummer.Avalonia.exe",
                "sha256": "9" * 64,
                "sizeBytes": 100,
            },
            "nativeHostEvidence": {
                "contractName": evidence.WINDOWS.NATIVE_HOST_CONTRACT,
                "evidenceSource": "GitHub-hosted windows-latest",
                "hostPlatform": "windows",
                "isNativeWindows": True,
                "runner": "pwsh",
                "status": "verified",
            },
            "source": source,
            "startupScreenshot": {
                "height": 200,
                "path": evidence.STARTUP_SCREENSHOT,
                "sha256": digest(root / evidence.STARTUP_SCREENSHOT),
                "width": 320,
            },
            "status": "captured",
        },
    )


def capture_fixture(
    tmp_path: Path,
) -> tuple[Path, Path, argparse.Namespace]:
    values = export_fixtures.fixture(tmp_path / "candidate")
    export_fixtures.exporter.export_candidate(values["args"])
    candidate = values["output"]
    native = tmp_path / "native"
    native.mkdir()
    source = source_binding()
    write_native_inputs(
        native,
        candidate,
        values["args"].expected_version,
        source,
    )
    args = argparse.Namespace(
        candidate_root=candidate,
        expected_version=values["args"].expected_version,
        expected_manifest_sha256=values[
            "args"
        ].expected_manifest_sha256,
        expected_inventory_sha256=digest(
            candidate / evidence.EXPORT.CONTENT_INVENTORY_PATH
        ),
        candidate_source_sha=values["args"].source_sha,
        candidate_run_id=values["args"].source_run_id,
        candidate_run_attempt=values["args"].source_run_attempt,
        candidate_actor=values["args"].source_actor,
        candidate_artifact_id="101",
        candidate_artifact_name=(
            "unsigned-windows-preview-nightly-candidate-123456-1"
        ),
        candidate_artifact_sha256="a" * 64,
        evidence_root=native,
        capture_repository=source["repository"],
        capture_workflow=source["workflow"],
        capture_run_id=source["runId"],
        capture_run_attempt=source["runAttempt"],
        capture_ref=source["ref"],
        capture_sha=source["sha"],
        capture_actor=source["actor"],
        capture_triggering_actor=source["triggeringActor"],
        output_artifact_name=source["artifactName"],
    )
    evidence.capture(args)
    return candidate, native, args


def finalize_args(native: Path, output: Path) -> argparse.Namespace:
    source = source_binding()
    return argparse.Namespace(
        capture_root=native,
        output_root=output,
        capture_inventory_sha256=digest(
            native / evidence.CAPTURE_INVENTORY_FILE
        ),
        expected_capture_repository=source["repository"],
        expected_capture_workflow=source["workflow"],
        expected_capture_run_id=source["runId"],
        expected_capture_run_attempt=source["runAttempt"],
        expected_capture_ref=source["ref"],
        expected_capture_sha=source["sha"],
        expected_capture_actor=source["actor"],
        expected_capture_artifact_id="202",
        expected_capture_artifact_name=source["artifactName"],
        expected_capture_artifact_sha256="b" * 64,
        accountable_review_confirmed="true",
        review_json=json.dumps(
            {key: True for key in evidence.EXPECTED_REVIEW_KEYS},
            sort_keys=True,
        ),
        reviewer_id=evidence.REVIEWER_ID,
        reviewer_kind=evidence.REVIEWER_KIND,
        finalization_repository=evidence.SOURCE_REPOSITORY,
        finalization_workflow=evidence.FINALIZE_WORKFLOW,
        finalization_run_id="30001",
        finalization_run_attempt="1",
        finalization_ref=evidence.SOURCE_REF,
        finalization_sha=CAPTURE_SHA,
        finalization_actor=evidence.REVIEWER_ID,
        finalization_triggering_actor=evidence.REVIEWER_ID,
        finalization_artifact_name=(
            "unsigned-windows-preview-native-evidence-finalized-"
            "30001-1"
        ),
    )


def test_exact_unsigned_candidate_can_be_captured_and_accountably_finalized(
    tmp_path: Path,
) -> None:
    candidate, native, capture_args = capture_fixture(tmp_path)
    assert capture_args.candidate_source_sha != capture_args.capture_sha
    output = tmp_path / "finalized"
    result = evidence.finalize(finalize_args(native, output))

    finalization = json.loads(
        (output / evidence.FINALIZATION_FILE).read_text(encoding="utf-8")
    )
    assert finalization["status"] == "passed"
    assert finalization["accountableReviewConfirmed"] is True
    assert finalization["reviewer"] == "ArchonMegalon"
    assert finalization["reviewerKind"] == (
        "authenticated_account_owner_delegated_operator"
    )
    assert finalization["reviewerWasCaptureActor"] is False
    assert finalization["confirmations"] == {
        key: "passed" for key in sorted(evidence.EXPECTED_REVIEW_KEYS)
    }
    assert "human_review" not in json.dumps(
        finalization, sort_keys=True
    ).lower()
    for key in (
        "deployAuthorized",
        "publicationAuthorized",
        "uiUploadAuthorized",
        "uploadAuthorized",
    ):
        assert finalization[key] is False

    proof = json.loads(
        (output / evidence.VISUAL_PROOF_FILE).read_text(encoding="utf-8")
    )
    assert proof["checks"] == {
        "accountable_review_confirmed": True,
        "capture_mode": "hosted_native_windows",
    }
    assert proof["review"]["allowlistSource"] == (
        "pinned contract identity plus protected environment and "
        "authenticated workflow actor"
    )
    assert [row["role"] for row in proof["screenshots"]] == [
        "startup",
        "progress",
        "completion",
    ]
    assert proof["authenticodeVerification"] == {
        "path": evidence.AUTHENTICODE_FILE,
        "sha256": digest(output / evidence.AUTHENTICODE_FILE),
        "signatureStatus": "unsigned",
        "signingRequired": False,
        "sizeBytes": (output / evidence.AUTHENTICODE_FILE).stat().st_size,
        "unsignedReason": "preview_policy",
    }

    outer = json.loads(
        (output / evidence.FINALIZED_EVIDENCE_FILE).read_text(
            encoding="utf-8"
        )
    )
    assert set(outer) == {
        "candidateContentInventory",
        "candidateContentInventorySha256",
        "captureGeneratedAtUtc",
        "captureSource",
        "files",
        "finalizationGeneratedAtUtc",
        "finalizationSource",
        "reviewer",
        "status",
    }
    assert outer["status"] == "passed"
    assert outer["reviewer"] == "ArchonMegalon"
    assert outer["captureSource"]["actor"] == "github-actions[bot]"
    assert outer["finalizationSource"]["actor"] == "ArchonMegalon"
    assert outer["captureSource"]["sha"] == CAPTURE_SHA
    assert outer["finalizationSource"]["sha"] == CAPTURE_SHA
    assert set(outer["captureSource"]) == {
        "actor",
        "artifactName",
        "ref",
        "repository",
        "runAttempt",
        "runId",
        "sha",
        "workflow",
    }
    inventory_path = (
        output
        / evidence.CANDIDATE_PROVENANCE_DIRECTORY
        / evidence.EXPORT.CONTENT_INVENTORY_PATH
    )
    assert outer["candidateContentInventory"] == json.loads(
        inventory_path.read_text(encoding="utf-8")
    )
    assert outer["candidateContentInventorySha256"] == digest(
        inventory_path
    )
    assert outer["candidateContentInventory"]["sourceSha"] == (
        export_fixtures.scope_fixtures.SOURCE_SHA
    )
    assert outer["candidateContentInventory"]["sourceSha"] != CAPTURE_SHA
    finalized_inventory = json.loads(
        (output / evidence.FINALIZED_INVENTORY_FILE).read_text(
            encoding="utf-8"
        )
    )
    assert finalized_inventory["files"] == evidence.exact_inventory(
        output,
        exclude={
            evidence.FINALIZED_EVIDENCE_FILE,
            evidence.FINALIZED_INVENTORY_FILE,
        },
    )
    capture_inventory = json.loads(
        (output / evidence.CAPTURE_INVENTORY_FILE).read_text(
            encoding="utf-8"
        )
    )
    finalized_capture_rows = [
        row
        for row in finalized_inventory["files"]
        if row["path"]
        not in {
            evidence.CAPTURE_INVENTORY_FILE,
            evidence.FINALIZATION_FILE,
            evidence.VISUAL_PROOF_FILE,
        }
    ]
    assert finalized_capture_rows == capture_inventory["files"]
    outer_paths = [row["path"] for row in outer["files"]]
    assert outer_paths == sorted(outer_paths)
    for required in (
        evidence.CAPTURE_FILE,
        evidence.CAPTURE_INVENTORY_FILE,
        evidence.FINALIZATION_FILE,
        evidence.FINALIZED_INVENTORY_FILE,
        evidence.VISUAL_PROOF_FILE,
        evidence.AUTHENTICODE_FILE,
        evidence.STARTUP_VISUAL_RECEIPT,
        evidence.STARTUP_SCREENSHOT,
        (
            f"{evidence.CANDIDATE_PROVENANCE_DIRECTORY}/"
            f"{evidence.EXPORT.CONTENT_INVENTORY_PATH}"
        ),
        (
            f"{evidence.CANDIDATE_PROVENANCE_DIRECTORY}/"
            f"{evidence.EXPORT.EXPORT_RECEIPT_PATH}"
        ),
    ):
        assert required in outer_paths
    assert evidence.FINALIZED_EVIDENCE_FILE not in outer_paths
    for row in outer["files"]:
        raw = base64.b64decode(row["bytesBase64"], validate=True)
        assert len(raw) == row["sizeBytes"]
        assert hashlib.sha256(raw).hexdigest() == row["sha256"]
        assert raw == (output / row["path"]).read_bytes()
    assert result["native_evidence_file_sha256"] == digest(
        output / evidence.FINALIZED_EVIDENCE_FILE
    )
    assert result["native_evidence_sha256"] == (
        evidence.compact_json_sha256(outer)
    )
    assert candidate.is_dir()


@pytest.mark.parametrize(
    ("relative_path", "required_marker"),
    [
        (evidence.STARTUP_LOG, "native startup passed"),
        (
            evidence.PAYLOAD_HTTP_LOG,
            "candidate payload download passed",
        ),
    ],
)
def test_capture_rejects_missing_required_native_checkpoint_marker(
    tmp_path: Path,
    relative_path: str,
    required_marker: str,
) -> None:
    _, native, capture_args = capture_fixture(tmp_path)
    (native / relative_path).write_text(
        "native operation observed without completion checkpoint\n",
        encoding="utf-8",
    )

    with pytest.raises(
        evidence.EvidenceError,
        match=f"omits required marker: {re.escape(required_marker)}",
    ):
        evidence.validate_native_evidence(
            native,
            evidence.candidate_bindings(capture_args),
            source_binding(),
            require_exact_root=False,
        )


def test_candidate_provenance_chmod_is_portable_without_no_follow_support(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    values = export_fixtures.fixture(tmp_path / "candidate")
    export_fixtures.exporter.export_candidate(values["args"])
    native = tmp_path / "native"
    native.mkdir()
    actual_chmod = evidence.os.chmod
    calls: list[bool] = []

    def windows_compatible_chmod(
        path: Path,
        mode: int,
        *,
        follow_symlinks: bool = True,
    ) -> None:
        calls.append(follow_symlinks)
        if follow_symlinks is False:
            raise NotImplementedError(
                "chmod: follow_symlinks unavailable on this platform"
            )
        actual_chmod(path, mode)

    monkeypatch.setattr(evidence.os, "chmod", windows_compatible_chmod)
    rows = evidence.copy_candidate_provenance(
        values["output"], native
    )

    assert calls == [False, True, False, True]
    assert {row["path"] for row in rows} == {
        (
            f"{evidence.CANDIDATE_PROVENANCE_DIRECTORY}/"
            f"{evidence.EXPORT.CONTENT_INVENTORY_PATH}"
        ),
        (
            f"{evidence.CANDIDATE_PROVENANCE_DIRECTORY}/"
            f"{evidence.EXPORT.EXPORT_RECEIPT_PATH}"
        ),
    }


def test_new_evidence_json_is_portable_without_descriptor_chmod(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    output = tmp_path / "evidence.json"
    monkeypatch.delattr(evidence.os, "fchmod")

    evidence.write_json_new(output, {"status": "captured"})

    assert json.loads(output.read_text(encoding="utf-8")) == {
        "status": "captured"
    }
    assert output.stat().st_mode & 0o222 == 0
    with pytest.raises(FileExistsError):
        evidence.write_json_new(output, {"status": "replaced"})


def test_main_reports_shared_windows_contract_errors_without_traceback(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    monkeypatch.setattr(
        evidence,
        "parse_args",
        lambda _argv: argparse.Namespace(command="capture"),
    )

    def reject(_args: argparse.Namespace) -> dict[str, str]:
        raise evidence.WINDOWS.ContractError("exact receipt binding differs")

    monkeypatch.setattr(evidence, "capture", reject)

    assert evidence.main([]) == 2
    captured = capsys.readouterr()
    assert captured.out == ""
    assert captured.err == (
        "unsigned-windows-native-evidence:error: "
        "exact receipt binding differs\n"
    )


def test_finalization_rejects_any_capture_byte_tamper(
    tmp_path: Path,
) -> None:
    _, native, _ = capture_fixture(tmp_path)
    screenshot = native / evidence.STARTUP_SCREENSHOT
    screenshot.write_bytes(screenshot.read_bytes() + b"tamper")
    with pytest.raises(
        evidence.EvidenceError,
        match="capture inventory files",
    ):
        evidence.finalize(finalize_args(native, tmp_path / "finalized"))


@pytest.mark.parametrize(
    ("mutation", "message"),
    [
        ("reviewer", "sole pinned ArchonMegalon"),
        ("unconfirmed", "accountable review confirmation"),
        ("false-check", "must be true"),
        ("extra-check", "missing or extra fields"),
    ],
)
def test_finalization_rejects_unaccountable_or_inexact_review(
    tmp_path: Path, mutation: str, message: str
) -> None:
    _, native, _ = capture_fixture(tmp_path)
    args = finalize_args(native, tmp_path / "finalized")
    if mutation == "reviewer":
        args.reviewer_id = "different-reviewer"
    elif mutation == "unconfirmed":
        args.accountable_review_confirmed = "false"
    else:
        review = json.loads(args.review_json)
        if mutation == "false-check":
            review["startup"] = False
        else:
            review["other"] = True
        args.review_json = json.dumps(review, sort_keys=True)
    with pytest.raises(evidence.EvidenceError, match=message):
        evidence.finalize(args)


def test_capture_requires_distinct_hosted_automation_actor(
    tmp_path: Path,
) -> None:
    values = export_fixtures.fixture(tmp_path / "candidate")
    export_fixtures.exporter.export_candidate(values["args"])
    native = tmp_path / "native"
    native.mkdir()
    source = source_binding()
    source["actor"] = "ArchonMegalon"
    source["triggeringActor"] = "ArchonMegalon"
    write_native_inputs(
        native,
        values["output"],
        values["args"].expected_version,
        source,
    )
    args = argparse.Namespace(
        candidate_root=values["output"],
        expected_version=values["args"].expected_version,
        expected_manifest_sha256=values[
            "args"
        ].expected_manifest_sha256,
        expected_inventory_sha256=digest(
            values["output"] / evidence.EXPORT.CONTENT_INVENTORY_PATH
        ),
        candidate_source_sha=values["args"].source_sha,
        candidate_run_id=values["args"].source_run_id,
        candidate_run_attempt=values["args"].source_run_attempt,
        candidate_actor=values["args"].source_actor,
        candidate_artifact_id="101",
        candidate_artifact_name=(
            "unsigned-windows-preview-nightly-candidate-123456-1"
        ),
        candidate_artifact_sha256="a" * 64,
        evidence_root=native,
        capture_repository=source["repository"],
        capture_workflow=source["workflow"],
        capture_run_id=source["runId"],
        capture_run_attempt=source["runAttempt"],
        capture_ref=source["ref"],
        capture_sha=source["sha"],
        capture_actor=source["actor"],
        capture_triggering_actor=source["triggeringActor"],
        output_artifact_name=source["artifactName"],
    )
    with pytest.raises(
        evidence.EvidenceError, match="capture automation actor"
    ):
        evidence.capture(args)
