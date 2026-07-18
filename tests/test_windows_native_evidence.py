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
CANDIDATE_SHA = "b" * 40


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


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
    write_json(manifest, {"version": VERSION, "channelId": CHANNEL, "artifacts": rows})
    args = argparse.Namespace(
        candidate_root=candidate,
        candidate_manifest=manifest.name,
        candidate_manifest_sha256=digest(manifest),
        evidence_root=native,
        version=VERSION,
        channel=CHANNEL,
        avalonia_installer=bindings["avalonia"]["installer"],
        avalonia_installer_sha256=bindings["avalonia"]["installer_sha256"],
        avalonia_payload=bindings["avalonia"]["payload"],
        avalonia_payload_sha256=bindings["avalonia"]["payload_sha256"],
        blazor_desktop_installer=bindings["blazor-desktop"]["installer"],
        blazor_desktop_installer_sha256=bindings["blazor-desktop"]["installer_sha256"],
        blazor_desktop_payload=bindings["blazor-desktop"]["payload"],
        blazor_desktop_payload_sha256=bindings["blazor-desktop"]["payload_sha256"],
        source_repository="chummer6/chummer6-ui",
        source_workflow=".github/workflows/windows-native-evidence-capture.yml",
        source_run_id="12345",
        source_run_attempt="1",
        source_ref="refs/heads/codex/native-evidence",
        source_sha=CAPTURE_SHA,
        source_actor="capture-operator",
        output_artifact_name="windows-native-evidence-12345-1",
        candidate_repository="chummer6/chummer6-ui",
        candidate_workflow=".github/workflows/preview-stage.yml",
        candidate_run_id="12000",
        candidate_ref="refs/heads/candidate",
        candidate_sha=CANDIDATE_SHA,
        candidate_artifact_name="preview-stage-12000",
    )
    return candidate, native, args


def finalize_args(native: Path, output: Path) -> argparse.Namespace:
    return argparse.Namespace(
        capture_root=native,
        output_root=output,
        capture_inventory_sha256=digest(native / evidence.CAPTURE_INVENTORY_FILE),
        expected_repository="chummer6/chummer6-ui",
        expected_workflow=".github/workflows/windows-native-evidence-capture.yml",
        expected_run_id="12345",
        expected_run_attempt="1",
        expected_ref="refs/heads/codex/native-evidence",
        expected_sha=CAPTURE_SHA,
        expected_capture_actor="capture-operator",
        expected_artifact_name="windows-native-evidence-12345-1",
        reviewer_id="accountable-reviewer",
        reviewer_allowlist_json='["accountable-reviewer", "backup-reviewer"]',
        human_review_confirmed="true",
        finalization_repository="chummer6/chummer6-ui",
        finalization_workflow=evidence.FINALIZE_WORKFLOW,
        finalization_run_id="13000",
        finalization_run_attempt="1",
        finalization_ref="refs/heads/codex/native-evidence",
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
    _, native, args = make_fixture(tmp_path)
    evidence.capture(args)
    output = tmp_path / "finalized"
    evidence.finalize(finalize_args(native, output))

    assert (output / evidence.CAPTURE_INVENTORY_FILE).is_file()
    assert (output / evidence.FINALIZED_INVENTORY_FILE).is_file()
    finalized_inventory = json.loads((output / evidence.FINALIZED_INVENTORY_FILE).read_text(encoding="utf-8"))
    assert finalized_inventory["files"] == evidence.exact_inventory(
        output, exclude={evidence.FINALIZED_INVENTORY_FILE}
    )
    finalization = json.loads((output / evidence.FINALIZATION_FILE).read_text(encoding="utf-8"))
    assert finalization["finalizationSource"] == {
        "repository": "chummer6/chummer6-ui",
        "workflow": evidence.FINALIZE_WORKFLOW,
        "runId": "13000",
        "runAttempt": "1",
        "ref": "refs/heads/codex/native-evidence",
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


def test_preflight_accepts_exact_candidate_bytes_without_writing_evidence(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.preflight(args)

    output = capsys.readouterr().out
    assert f"candidate_manifest_sha256={args.candidate_manifest_sha256}" in output
    assert not (native / evidence.CAPTURE_FILE).exists()
    assert not (native / evidence.CAPTURE_INVENTORY_FILE).exists()


@pytest.mark.parametrize("target", ["installer", "payload", "manifest"])
def test_preflight_rejects_tampered_candidate_bytes(tmp_path: Path, target: str) -> None:
    candidate, _, args = make_fixture(tmp_path)
    if target == "manifest":
        (candidate / args.candidate_manifest).write_text("{}\n", encoding="utf-8")
    else:
        relative = getattr(args, f"avalonia_{target}")
        (candidate / relative).write_bytes(b"tampered")
    with pytest.raises(evidence.ContractError, match="do not match"):
        evidence.preflight(args)


@pytest.mark.parametrize("target", ["installer", "payload", "manifest"])
def test_capture_rejects_tampered_candidate_bytes(tmp_path: Path, target: str) -> None:
    candidate, _, args = make_fixture(tmp_path)
    if target == "manifest":
        (candidate / args.candidate_manifest).write_text("{}\n", encoding="utf-8")
    else:
        relative = getattr(args, f"avalonia_{target}")
        (candidate / relative).write_bytes(b"tampered")
    with pytest.raises(evidence.ContractError, match="do not match"):
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


def test_finalize_rejects_inventory_tampering(tmp_path: Path) -> None:
    _, native, args = make_fixture(tmp_path)
    evidence.capture(args)
    finalize = finalize_args(native, tmp_path / "finalized")
    (native / evidence.head_paths("avalonia")["progressLog"]).write_text("tampered\n", encoding="utf-8")
    with pytest.raises(evidence.ContractError, match="inventory"):
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


def test_workflow_run_path_matcher_accepts_only_exact_bound_shapes() -> None:
    helper = REPO_ROOT / "scripts/github_workflow_run_path.js"
    bare = ".github/workflows/windows-native-evidence-capture.yml"
    branch = "codex/native-evidence"
    ref = f"refs/heads/{branch}"
    sha = CAPTURE_SHA
    cases = [
        {"actual": bare, "expected": True},
        {"actual": f"{bare}@{branch}", "expected": True},
        {"actual": f"{bare}@{ref}", "expected": True},
        {"actual": f"{bare}@{sha}", "expected": True},
        {"actual": f"{bare}@refs/tags/{branch}", "expected": True},
        {"actual": f"{bare}@refs/heads/other", "expected": False},
        {"actual": f"{bare}@{branch}-other", "expected": False},
        {"actual": f"{bare}@{sha}0", "expected": False},
        {"actual": f"{bare}-other@{branch}", "expected": False},
        {"actual": f"{bare}@{branch}/other", "expected": False},
    ]
    script = """
    const { workflowRunPathMatches } = require(process.argv[1]);
    const cases = JSON.parse(process.argv[2]);
    const source = JSON.parse(process.argv[3]);
    process.stdout.write(JSON.stringify(cases.map(row =>
      workflowRunPathMatches(row.actual, row.bare, source))));
    """
    rows = [{**row, "bare": bare} for row in cases]
    completed = subprocess.run(
        [
            "node",
            "-e",
            script,
            str(helper),
            json.dumps(rows),
            json.dumps({"branch": branch, "ref": ref, "sha": sha}),
        ],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    assert json.loads(completed.stdout) == [row["expected"] for row in cases]

    tag_source = {"branch": "v1.2.3", "ref": "refs/tags/v1.2.3", "sha": sha}
    tag_completed = subprocess.run(
        [
            "node",
            "-e",
            script,
            str(helper),
            json.dumps([{"actual": f"{bare}@refs/tags/v1.2.3", "bare": bare}]),
            json.dumps(tag_source),
        ],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    assert json.loads(tag_completed.stdout) == [True]


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
    for path in (capture_path, finalize_path):
        workflow = yaml.load(path.read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
        assert len(workflow["on"]["workflow_dispatch"]["inputs"]) <= 10
        for job in workflow["jobs"].values():
            for step in job["steps"]:
                if "uses" in step:
                    assert re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}", step["uses"])
    capture_workflow = yaml.load(capture, Loader=yaml.BaseLoader)
    capture_steps = capture_workflow["jobs"]["capture"]["steps"]
    step_names = [step["name"] for step in capture_steps]
    assert step_names.index("Check out evidence contract") < step_names.index(
        "Authenticate exact candidate run and artifact"
    )
    download_index = step_names.index("Download only the named candidate artifact")
    preflight_index = step_names.index("Preflight exact candidate bytes before execution")
    assert preflight_index == download_index + 1
    for executable_step in (
        "Native startup receipt - Avalonia",
        "Native startup receipt - Blazor Desktop",
        "Capture interactive Avalonia progress and completion",
        "Capture interactive Blazor Desktop progress and completion",
    ):
        assert preflight_index < step_names.index(executable_step)
    preflight_run = capture_steps[preflight_index]["run"]
    assert "windows_native_evidence.py preflight" in preflight_run
    for binding in (
        "--candidate-manifest-sha256",
        "--avalonia-installer-sha256",
        "--avalonia-payload-sha256",
        "--blazor-desktop-installer-sha256",
        "--blazor-desktop-payload-sha256",
    ):
        assert binding in preflight_run
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
