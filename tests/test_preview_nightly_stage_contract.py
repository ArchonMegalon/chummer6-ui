from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import binascii
import shutil
import struct
import subprocess
import sys
import tempfile
import zipfile
import zlib
from datetime import UTC, datetime
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
HELPER_PATH = REPO_ROOT / "scripts" / "preview_nightly_stage_contract.py"
ORCHESTRATOR_PATH = REPO_ROOT / "scripts" / "build-preview-nightly-stage.sh"
MANIFEST_GENERATOR_PATH = REPO_ROOT / "scripts" / "generate-releases-manifest.sh"


def load_helper():
    spec = importlib.util.spec_from_file_location("preview_nightly_stage_contract", HELPER_PATH)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


MODULE = load_helper()


def load_registry_fixture_module():
    registry_path = (
        REPO_ROOT.parent
        / "chummer-hub-registry"
        / "scripts"
        / "materialize_public_release_channel.py"
    )
    spec = importlib.util.spec_from_file_location("fixture_registry_materializer", registry_path)
    assert spec is not None and spec.loader is not None
    registry = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(registry)
    return registry


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def write_png(path: Path, color: tuple[int, int, int]) -> None:
    width, height = 320, 200
    raw = b"".join(b"\x00" + bytes(color) * width for _ in range(height))

    def chunk(kind: bytes, payload: bytes) -> bytes:
        return (
            struct.pack(">I", len(payload))
            + kind
            + payload
            + struct.pack(">I", binascii.crc32(kind + payload) & 0xFFFFFFFF)
        )

    path.write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw))
        + chunk(b"IEND", b"")
    )


def init_repo(path: Path, sentinel: str) -> str:
    path.mkdir(parents=True, exist_ok=True)
    subprocess.run(["git", "init", "-q", str(path)], check=True)
    subprocess.run(["git", "-C", str(path), "config", "user.name", "Fixture"], check=True)
    subprocess.run(["git", "-C", str(path), "config", "user.email", "fixture@example.invalid"], check=True)
    (path / "README.md").write_text("fixture\n", encoding="utf-8")
    sentinel_path = path / sentinel
    sentinel_path.parent.mkdir(parents=True, exist_ok=True)
    sentinel_path.write_text("fixture sentinel\n", encoding="utf-8")
    subprocess.run(["git", "-C", str(path), "add", "-A"], check=True)
    subprocess.run(["git", "-C", str(path), "commit", "-qm", "fixture"], check=True)
    return subprocess.check_output(["git", "-C", str(path), "rev-parse", "HEAD"], text=True).strip()


def configure_authorities(monkeypatch: pytest.MonkeyPatch, root: Path) -> Path:
    workspace = root / "workspace"
    presentation_root = workspace / "presentation"
    authority_paths = {
        "presentation": presentation_root,
        "core": workspace / "chummer-core-engine",
        "run": workspace / "chummer.run-services",
        "ui-kit": workspace / "chummer-ui-kit",
        "registry": workspace / "chummer-hub-registry",
        "media-factory": workspace / "fleet" / "repos" / "chummer-media-factory",
        "legacy": root / "chummer5a",
    }
    for name, root_env, commit_env in MODULE.AUTHORITY_ENVIRONMENTS:
        repo = authority_paths[name]
        if name == "presentation":
            (repo / "scripts").mkdir(parents=True, exist_ok=True)
            for script_name in (
                "materialize-windows-desktop-exit-gate.sh",
                "verify-windows-release-evidence.py",
                "materialize_release_candidate_handoff.py",
                "materialize_windows_visual_proof_handoff.py",
            ):
                shutil.copy2(
                    REPO_ROOT / "scripts" / script_name,
                    repo / "scripts" / script_name,
                )
            workflow_root = repo / ".github" / "workflows"
            workflow_root.mkdir(parents=True, exist_ok=True)
            (workflow_root / "windows-native-evidence-capture.yml").write_text(
                "name: fixture native capture\n", encoding="utf-8"
            )
            (workflow_root / "windows-native-evidence-finalize.yml").write_text(
                "name: fixture native finalization\n", encoding="utf-8"
            )
        elif name == "registry":
            (repo / "scripts").mkdir(parents=True, exist_ok=True)
            for script_name in (
                "materialize_public_release_channel.py",
                "verify_public_release_channel.py",
            ):
                shutil.copy2(
                    REPO_ROOT.parent / "chummer-hub-registry" / "scripts" / script_name,
                    repo / "scripts" / script_name,
                )
        commit = init_repo(repo, MODULE.AUTHORITY_SENTINELS[name])
        if name == "presentation":
            subprocess.run(
                [
                    "git",
                    "-C",
                    str(repo),
                    "remote",
                    "add",
                    "origin",
                    "https://github.com/fixture/chummer6-ui.git",
                ],
                check=True,
            )
        monkeypatch.setenv(root_env, str(repo))
        monkeypatch.setenv(commit_env, commit)
    return presentation_root


def valid_proof_input_payload(input_name: str) -> dict:
    generated_at = "2026-07-18T12:00:00Z"
    if input_name in {"hubLocalReleaseProof", "uiLocalizationReleaseGate"}:
        registry = load_registry_fixture_module()
        localization_domains = (
            "app_chrome",
            "install_update_support",
            "explain_receipts",
            "data_rules_names",
            "generated_artifacts",
        )
        localization = {
            "contract_name": "chummer6-ui.localization_release_gate",
            "status": "passed",
            "generatedAt": generated_at,
            "defaultKeyCount": 100,
            "explicitFallbackRuntime": "passed",
            "signoffSmokeRunnerStatus": "passed",
            "shippingLocales": list(registry.REQUIRED_LOCALIZATION_SHIPPING_LOCALES),
            "acceptanceGates": list(registry.REQUIRED_LOCALIZATION_ACCEPTANCE_GATES),
            "domainCoverage": {domain: "passed" for domain in localization_domains},
            "localeDomainCoverage": {
                locale: {domain: "passed" for domain in localization_domains}
                for locale in registry.REQUIRED_LOCALIZATION_SHIPPING_LOCALES
            },
            "blockingFindings": [],
            "blockingFindingsCount": 0,
            "translationBacklogFindings": [],
            "translationBacklogFindingsCount": 0,
            "localeSummary": [
                {
                    "locale": locale,
                    "untranslatedKeyCount": 0,
                    "overrideCount": 1,
                    "minimumOverrideCount": 1,
                    "missingReleaseSeedKeys": [],
                    "legacyXmlPresent": True,
                    "legacyDataXmlPresent": True,
                }
                for locale in registry.REQUIRED_LOCALIZATION_SHIPPING_LOCALES
            ],
        }
        if input_name == "uiLocalizationReleaseGate":
            return localization
        return {
            "status": "passed",
            "generatedAt": generated_at,
            "baseUrl": "https://chummer.run",
            "journeysPassed": list(registry.REQUIRED_RELEASE_PROOF_JOURNEYS),
            "proofRoutes": [
                *registry.REQUIRED_RELEASE_PROOF_ROUTES,
                "/downloads/install/avalonia-win-x64-installer",
                "/downloads/install/blazor-desktop-win-x64-installer",
            ],
            "uiLocalizationReleaseGate": localization,
        }
    target_name = next(
        row[3] for row in MODULE.EXACT_PROOF_INPUTS if row[0] == input_name
    )
    source = REPO_ROOT / ".codex-studio" / "published" / target_name
    payload = json.loads(source.read_text(encoding="utf-8-sig"))
    if "generatedAt" in payload:
        payload["generatedAt"] = generated_at
    if "generated_at" in payload:
        payload["generated_at"] = generated_at
    for key in ("channel", "channelId"):
        if key in payload:
            payload[key] = "preview"
    for key in ("version", "releaseVersion"):
        if key in payload:
            payload[key] = "run-fixture-1"
    if input_name in MODULE.UPSTREAM_PROOF_CONTRACTS:
        payload["status"] = "pass"
        for blocker_field in ("reasons", "blockingFindings", "blocking_findings", "blockers"):
            if isinstance(payload.get(blocker_field), list):
                payload[blocker_field] = []
    return payload


def configure_proof_inputs(monkeypatch: pytest.MonkeyPatch, root: Path) -> None:
    proof_root = root / "proof-inputs"
    for input_name, path_env, sha_env, target_name in MODULE.EXACT_PROOF_INPUTS:
        path = proof_root / target_name
        write_json(path, valid_proof_input_payload(input_name))
        monkeypatch.setenv(path_env, str(path))
        monkeypatch.setenv(sha_env, sha256(path))


def configure_retained_shelf(monkeypatch: pytest.MonkeyPatch, root: Path) -> tuple[Path, Path]:
    shelf = root / "retained"
    artifact = shelf / "files" / "chummer-avalonia-osx-arm64-installer.dmg"
    artifact.parent.mkdir(parents=True)
    artifact.write_bytes(b"retained-macos-installer")
    canonical = shelf / "RELEASE_CHANNEL.generated.json"
    releases = shelf / "releases.json"
    write_json(
        canonical,
        {
            "contractName": "Chummer.Hub.Registry.Contracts",
            "version": "incumbent-v1",
            "channelId": "preview",
            "artifacts": [
                {
                    "artifactId": "avalonia-osx-arm64-installer",
                    "head": "avalonia",
                    "platform": "macos",
                    "rid": "osx-arm64",
                    "kind": "installer",
                    "fileName": artifact.name,
                    "sha256": sha256(artifact),
                    "sizeBytes": artifact.stat().st_size,
                }
            ],
        },
    )
    write_json(
        releases,
        {
            "contractName": "Chummer.Hub.Registry.Contracts",
            "version": "incumbent-v1",
            "channel": "preview",
            "downloads": [
                {
                    "artifactId": "avalonia-osx-arm64-installer",
                    "head": "avalonia",
                    "platform": "macos",
                    "rid": "osx-arm64",
                    "kind": "installer",
                    "fileName": artifact.name,
                    "sha256": sha256(artifact),
                    "sizeBytes": artifact.stat().st_size,
                }
            ],
        },
    )
    monkeypatch.setenv("CHUMMER_PREVIEW_NIGHTLY_RETAINED_SHELF_ROOT", str(shelf))
    monkeypatch.setenv("CHUMMER_PREVIEW_NIGHTLY_RETAINED_CANONICAL_PATH", str(canonical))
    monkeypatch.setenv("CHUMMER_PREVIEW_NIGHTLY_RETAINED_CANONICAL_SHA256", sha256(canonical))
    monkeypatch.setenv("CHUMMER_PREVIEW_NIGHTLY_RETAINED_RELEASES_PATH", str(releases))
    monkeypatch.setenv("CHUMMER_PREVIEW_NIGHTLY_RETAINED_RELEASES_SHA256", sha256(releases))
    return shelf, artifact


def configure_release_paths(monkeypatch: pytest.MonkeyPatch, root: Path, candidate: Path) -> Path:
    version = "run-fixture-1"
    stage = root / f"nightly-run-{version}"
    monkeypatch.setenv("CHUMMER_PREVIEW_NIGHTLY_VERSION", version)
    monkeypatch.setenv("CHUMMER_PREVIEW_NIGHTLY_PUBLISHED_AT", "2026-07-18T12:00:00Z")
    monkeypatch.setenv("CHUMMER_PREVIEW_NIGHTLY_CANDIDATE_DIR", str(candidate))
    monkeypatch.setenv("CHUMMER_PREVIEW_NIGHTLY_STAGE_DIR", str(stage))
    return stage


def test_prepare_inputs_pins_clean_authorities_and_hydrates_complete_shelf(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    configure_proof_inputs(monkeypatch, tmp_path)
    _, retained_artifact = configure_retained_shelf(monkeypatch, tmp_path)
    candidate = tmp_path / ".nightly-run-run-fixture-1.candidate"
    candidate.mkdir()
    stage = configure_release_paths(monkeypatch, tmp_path, candidate)

    payload = MODULE.prepare_inputs(presentation_root, candidate)

    assert payload["status"] == "validated"
    assert payload["output"]["sealedStageBasename"] == stage.name
    assert len(payload["authorities"]) == len(MODULE.AUTHORITY_ENVIRONMENTS)
    copied = candidate / "files" / retained_artifact.name
    assert copied.read_bytes() == retained_artifact.read_bytes()
    assert payload["retainedShelf"]["canonicalSha256"] == sha256(
        tmp_path / "retained" / "RELEASE_CHANNEL.generated.json"
    )
    assert set(payload["inputs"]) == {row[0] for row in MODULE.EXACT_PROOF_INPUTS}
    assert all("root" not in authority for authority in payload["authorities"])


def test_authority_validation_rejects_dirty_or_drifted_worktree(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    dirty_root = Path(os.environ["CHUMMER_CORE_ROOT"])
    (dirty_root / "untracked.txt").write_text("drift\n", encoding="utf-8")

    with pytest.raises(MODULE.ContractError, match="core authority root is not clean"):
        MODULE.validate_authorities(presentation_root)

    (dirty_root / "untracked.txt").unlink()
    monkeypatch.setenv("CHUMMER_CORE_EXPECTED_COMMIT", "0" * 40)
    with pytest.raises(MODULE.ContractError, match="core authority drift"):
        MODULE.validate_authorities(presentation_root)


def test_retained_shelf_digest_drift_fails_before_copy(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _, artifact = configure_retained_shelf(monkeypatch, tmp_path)
    artifact.write_bytes(b"mutated-after-manifest")
    candidate = tmp_path / "candidate"
    candidate.mkdir()

    with pytest.raises(MODULE.ContractError, match="digest mismatch"):
        MODULE.hydrate_retained_shelf(tmp_path / "retained", candidate)

    assert not (candidate / "files").exists()


def write_current_stage(stage: Path, *, native_windows: bool) -> dict[tuple[str, str, str], dict]:
    files = stage / "files"
    startup = stage / "startup-smoke"
    files.mkdir(parents=True)
    startup.mkdir()
    rows: list[dict] = []
    tuples: dict[tuple[str, str, str], dict] = {}
    extension = {"windows": "exe", "linux": "deb"}
    for head, platform, rid in MODULE.CURRENT_NIGHTLY_TUPLES:
        artifact = files / f"chummer-{head}-{rid}-installer.{extension[platform]}"
        artifact.write_bytes(f"{head}:{platform}:{rid}".encode())
        row = {
            "artifactId": f"{head}-{rid}-installer",
            "head": head,
            "platform": platform,
            "rid": rid,
            "kind": "installer",
            "fileName": artifact.name,
            "version": "run-fixture-1",
            "sha256": sha256(artifact),
            "sizeBytes": artifact.stat().st_size,
        }
        if platform == "windows":
            payload_file = files / f"chummer-{head}-{rid}-payload.zip"
            with zipfile.ZipFile(payload_file, "w") as archive:
                archive.writestr("Samples/Legacy/Soma-Career.chum5", "fixture")
                archive.writestr(f"{head}/fixture.txt", rid)
            row.update(
                {
                    "installerMode": "bootstrap",
                    "payloadAcquisitionMode": "download",
                    "payloadFileName": payload_file.name,
                    "payloadSha256": sha256(payload_file),
                    "payloadSizeBytes": payload_file.stat().st_size,
                }
            )
        rows.append(row)
        tuples[(head, platform, rid)] = row
        receipt = {
            "status": "pass",
            "headId": head,
            "platform": platform,
            "rid": rid,
            "artifactDigest": f"sha256:{row['sha256']}",
            "artifactFileName": row["fileName"],
            "version": "run-fixture-1",
            "releaseVersion": "run-fixture-1",
            "channelId": "preview",
        }
        if platform == "windows":
            receipt.update(
                {
                    "readyCheckpoint": "pre_ui_event_loop",
                    "bootstrapPayloadAcquisitionMode": "download",
                    "bootstrapPayloadFileName": row["payloadFileName"],
                    "bootstrapPayloadSha256": row["payloadSha256"],
                    "bootstrapPayloadSizeBytes": row["payloadSizeBytes"],
                    "executionEnvironment": (
                        "native_windows" if native_windows else "wine_compatibility"
                    ),
                    "hostClass": "windows-native",
                    "operatingSystem": "Windows 11",
                    "arch": "x64",
                    "completedAtUtc": datetime.now(UTC)
                    .replace(microsecond=0)
                    .isoformat()
                    .replace("+00:00", "Z"),
                    "nativeHostEvidence": (
                        {
                            "contractName": MODULE.NATIVE_WINDOWS_HOST_EVIDENCE_CONTRACT_NAME,
                            "status": "verified",
                            "isNativeWindows": True,
                            "hostPlatform": "windows",
                            "hostKernel": "Windows_NT",
                            "runner": "powershell",
                            "evidenceSource": "fixture-native-host",
                        }
                        if native_windows
                        else {
                            "contractName": MODULE.NATIVE_WINDOWS_HOST_EVIDENCE_CONTRACT_NAME,
                            "status": "not_native",
                            "isNativeWindows": False,
                            "hostPlatform": "linux",
                            "hostKernel": "linux",
                            "runner": "wine",
                            "evidenceSource": "fixture-compatibility-host",
                        }
                    ),
                }
            )
            (startup / f"windows-installer-progress-{head}-{rid}.log").write_text(
                "Bootstrap temp root: C:\\Temp\\Chummer6\\installer-temp\n"
                "Payload download target: "
                f"C:\\Temp\\Chummer6\\installer-temp\\{row['payloadFileName']}\n"
                "Downloading application files\n"
                "Downloading application files - 50% 1 MB/s\n"
                "Verifying payload size\n"
                "Verifying payload checksum\n"
                "Extracting application files\n"
                "Install complete\n",
                encoding="utf-8",
            )
        write_json(startup / f"startup-smoke-{head}-{rid}.receipt.json", receipt)
    registry = load_registry_fixture_module()
    with tempfile.TemporaryDirectory(prefix="preview-nightly-proof-fixture-") as temp:
        proof_path = Path(temp) / "HUB_LOCAL_RELEASE_PROOF.generated.json"
        write_json(proof_path, valid_proof_input_payload("hubLocalReleaseProof"))
        normalized_release_proof = registry.load_release_proof(proof_path)
    assert isinstance(normalized_release_proof, dict)
    manifest = {
        "contractName": "Chummer.Hub.Registry.Contracts",
        "version": "run-fixture-1",
        "channelId": "preview",
        "generatedAt": "2026-07-18T12:00:00Z",
        "publishedAt": "2026-07-18T12:00:00Z",
        "status": "published",
        "supportabilityState": "review_required",
        "releaseProof": normalized_release_proof,
        "artifacts": rows,
    }
    manifest["publicTrustMetrics"] = registry.expected_public_trust_metrics(manifest)
    manifest["registryBoundaryCoverage"] = registry.expected_registry_boundary_coverage(manifest)
    write_json(
        stage / "RELEASE_CHANNEL.generated.json",
        manifest,
    )
    downloads = []
    for row in rows:
        downloads.append(
            {
                **row,
                "channelId": "preview",
                "version": "run-fixture-1",
            }
        )
    write_json(
        stage / "releases.json",
        {
            "contractName": "Chummer.Hub.Registry.Contracts",
            "version": "run-fixture-1",
            "channel": "preview",
            "downloads": downloads,
        },
    )
    return tuples


def write_native_evidence_source(
    evidence: Path,
    stage: Path,
    tuples: dict[tuple[str, str, str], dict],
    *,
    reviewer: str = "fixture-reviewer",
) -> None:
    evidence_startup = evidence / "startup-smoke"
    evidence_startup.mkdir(parents=True)
    (evidence / "screenshots").mkdir()
    capture_source = {
        "repository": "fixture/chummer6-ui",
        "workflow": MODULE.NATIVE_CAPTURE_WORKFLOW,
        "runId": "1001",
        "runAttempt": "1",
        "ref": "refs/heads/main",
        "sha": os.environ["CHUMMER_UI_EXPECTED_COMMIT"],
        "actor": "capture-user",
        "artifactName": "windows-native-evidence-1001-1",
    }
    finalization_source = {
        "repository": "fixture/chummer6-ui",
        "workflow": MODULE.NATIVE_FINALIZATION_WORKFLOW,
        "runId": "2002",
        "runAttempt": "1",
        "ref": "refs/heads/main",
        "sha": os.environ["CHUMMER_UI_EXPECTED_COMMIT"],
        "actor": reviewer,
        "artifactName": "windows-native-evidence-finalized-2002-1",
    }
    capture_heads: list[dict] = []
    for head in ("avalonia", "blazor-desktop"):
        row = tuples[(head, "windows", "win-x64")]
        receipt_path = evidence_startup / f"startup-smoke-{head}-win-x64.receipt.json"
        progress_log = evidence_startup / f"windows-installer-progress-{head}-win-x64.log"
        write_json(receipt_path, {
            "status": "pass",
            "headId": head,
            "platform": "windows",
            "rid": "win-x64",
            "version": "run-fixture-1",
            "releaseVersion": "run-fixture-1",
            "channelId": "preview",
            "artifactFileName": row["fileName"],
            "artifactDigest": f"sha256:{row['sha256']}",
            "readyCheckpoint": "pre_ui_event_loop",
            "bootstrapPayloadAcquisitionMode": "download",
            "bootstrapPayloadFileName": row["payloadFileName"],
            "bootstrapPayloadSha256": row["payloadSha256"],
            "bootstrapPayloadSizeBytes": row["payloadSizeBytes"],
            "executionEnvironment": "native_windows",
            "hostClass": "windows-native",
            "operatingSystem": "Windows 11",
            "arch": "x64",
            "completedAtUtc": datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
            "nativeHostEvidence": {
                "contractName": MODULE.NATIVE_WINDOWS_HOST_EVIDENCE_CONTRACT_NAME,
                "status": "verified",
                "isNativeWindows": True,
                "hostPlatform": "windows",
                "hostKernel": "Windows_NT",
                "runner": "powershell",
                "evidenceSource": "fixture-native-host",
            },
        })
        progress_log.write_text(
            "Bootstrap temp root: C:\\Temp\\Chummer6\\installer-temp\n"
            "Payload download target: "
            f"C:\\Temp\\Chummer6\\installer-temp\\{row['payloadFileName']}\n"
            "Downloading application files\n"
            "Downloading application files - 50% 1 MB/s\n"
            "Verifying payload size\n"
            "Verifying payload checksum\n"
            "Extracting application files\n"
            "Install complete\n",
            encoding="utf-8",
        )
        progress = evidence / "screenshots" / f"windows-installer-{head}-win-x64-progress.png"
        completion = evidence / "screenshots" / f"windows-installer-{head}-win-x64-completion.png"
        base = 40 if head == "avalonia" else 120
        write_png(progress, (base, 20, 200))
        write_png(completion, (base, 200, 20))
        capture_heads.append({
            "headId": head,
            "rid": "win-x64",
            "installer": {
                "relativePath": f"files/{row['fileName']}",
                "fileName": row["fileName"],
                "sha256": row["sha256"],
                "sizeBytes": row["sizeBytes"],
            },
            "payload": {
                "relativePath": f"files/{row['payloadFileName']}",
                "fileName": row["payloadFileName"],
                "sha256": row["payloadSha256"],
                "sizeBytes": row["payloadSizeBytes"],
            },
            "receipt": {
                "path": f"startup-smoke/{receipt_path.name}",
                "sha256": sha256(receipt_path),
            },
            "progressLog": {
                "path": f"startup-smoke/{progress_log.name}",
                "sha256": sha256(progress_log),
            },
            "screenshots": [
                {"role": "progress", "path": f"screenshots/{progress.name}", "sha256": sha256(progress), "width": 320, "height": 200},
                {"role": "completion", "path": f"screenshots/{completion.name}", "sha256": sha256(completion), "width": 320, "height": 200},
            ],
        })
    capture = {
        "contractName": MODULE.NATIVE_CAPTURE_CONTRACT_NAME,
        "contractVersion": 1,
        "status": "captured",
        "captureMode": "interactive",
        "generatedAt": "2026-07-18T12:00:00Z",
        "version": "run-fixture-1",
        "channelId": "preview",
        "source": capture_source,
        "candidate": {
            "repository": "fixture/chummer6-ui",
            "workflow": ".github/workflows/candidate.yml",
            "runId": "900",
            "ref": "refs/heads/main",
            "sha": os.environ["CHUMMER_UI_EXPECTED_COMMIT"],
            "artifactName": "preview-candidate-900",
            "manifestPath": "RELEASE_CHANNEL.generated.json",
            "manifestSha256": sha256(stage / "RELEASE_CHANNEL.generated.json"),
        },
        "heads": capture_heads,
    }
    write_json(evidence / MODULE.NATIVE_CAPTURE_FILE_NAME, capture)
    capture_inventory = {
        "contractName": MODULE.NATIVE_CAPTURE_INVENTORY_CONTRACT_NAME,
        "contractVersion": 1,
        "captureContract": MODULE.NATIVE_CAPTURE_CONTRACT_NAME,
        "captureManifestSha256": sha256(evidence / MODULE.NATIVE_CAPTURE_FILE_NAME),
        "files": MODULE.inventory_tree(
            evidence, exclusions=(MODULE.NATIVE_CAPTURE_INVENTORY_FILE_NAME,)
        ),
    }
    write_json(evidence / MODULE.NATIVE_CAPTURE_INVENTORY_FILE_NAME, capture_inventory)
    capture_inventory_sha = sha256(evidence / MODULE.NATIVE_CAPTURE_INVENTORY_FILE_NAME)
    proof_rows: list[dict] = []
    for capture_head in capture_heads:
        head = capture_head["headId"]
        row = tuples[(head, "windows", "win-x64")]
        proof_path = evidence / f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json"
        write_json(
            proof_path,
            {
                "contractName": MODULE.WINDOWS_VISUAL_PROOF_CONTRACT_NAME,
                "contractVersion": 1,
                "status": "pass",
                "version": "run-fixture-1",
                "channelId": "preview",
                "head": head,
                "headId": head,
                "platform": "windows",
                "rid": "win-x64",
                "artifactFileName": row["fileName"],
                "artifactDigest": f"sha256:{row['sha256']}",
                "screenshots": [
                    {key: shot[key] for key in ("role", "path", "sha256")}
                    for shot in capture_head["screenshots"]
                ],
                "readabilityReview": {"status": "pass", "reviewer": reviewer},
                "contrastReview": {"status": "pass", "reviewer": reviewer},
                "clippingReview": {"status": "pass", "reviewer": reviewer},
                "checks": {"capture_mode": "interactive", "human_review_confirmed": True},
                "review": {
                    "authenticatedReviewer": reviewer,
                    "captureActor": capture_source["actor"],
                    "allowlistSource": "repository variable plus protected environment",
                    "explicitConfirmations": {"readability": "passed", "contrast": "passed", "clipping": "passed"},
                },
                "captureBinding": {**capture_source, "inventorySha256": capture_inventory_sha},
                "finalizationBinding": finalization_source,
            },
        )
        proof_rows.append({"headId": head, "path": proof_path.name, "sha256": sha256(proof_path)})
    write_json(evidence / MODULE.NATIVE_FINALIZATION_FILE_NAME, {
        "contractName": MODULE.NATIVE_FINALIZATION_CONTRACT_NAME,
        "contractVersion": 1,
        "status": "passed",
        "generatedAt": "2026-07-18T12:05:00Z",
        "captureInventorySha256": capture_inventory_sha,
        "captureSource": capture_source,
        "finalizationSource": finalization_source,
        "reviewer": reviewer,
        "reviewerWasCaptureActor": False,
        "humanReviewConfirmed": True,
        "proofs": proof_rows,
    })
    write_json(evidence / MODULE.NATIVE_FINALIZED_INVENTORY_FILE_NAME, {
        "contractName": MODULE.NATIVE_FINALIZED_INVENTORY_CONTRACT_NAME,
        "contractVersion": 1,
        "captureInventorySha256": capture_inventory_sha,
        "files": MODULE.inventory_tree(
            evidence, exclusions=(MODULE.NATIVE_FINALIZED_INVENTORY_FILE_NAME,)
        ),
    })


def zip_evidence(evidence: Path, archive: Path) -> None:
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_STORED) as bundle:
        for path in MODULE.safe_tree_entries(evidence):
            bundle.write(path, path.relative_to(evidence).as_posix())


def configure_github_api(
    monkeypatch: pytest.MonkeyPatch,
    archive: Path,
) -> None:
    runs = {
        "1001": {
            "workflow": MODULE.NATIVE_CAPTURE_WORKFLOW,
            "actor": "capture-user",
            "artifact": "windows-native-evidence-1001-1",
            "artifact_id": 501,
            "digest": "c" * 64,
        },
        "2002": {
            "workflow": MODULE.NATIVE_FINALIZATION_WORKFLOW,
            "actor": "fixture-reviewer",
            "artifact": "windows-native-evidence-finalized-2002-1",
            "artifact_id": 502,
            "digest": sha256(archive),
        },
    }

    def fetch(url: str) -> dict:
        run_id = next((value for value in runs if f"/runs/{value}" in url), "")
        assert run_id
        row = runs[run_id]
        if url.endswith("/artifacts?per_page=100"):
            return {
                "total_count": 1,
                "artifacts": [
                    {
                        "id": row["artifact_id"],
                        "name": row["artifact"],
                        "expired": False,
                        "digest": f"sha256:{row['digest']}",
                        "workflow_run": {
                            "id": int(run_id),
                            "head_sha": os.environ["CHUMMER_UI_EXPECTED_COMMIT"],
                        },
                    }
                ],
            }
        return {
            "id": int(run_id),
            "path": row["workflow"],
            "head_sha": os.environ["CHUMMER_UI_EXPECTED_COMMIT"],
            "run_attempt": 1,
            "event": "workflow_dispatch",
            "status": "completed",
            "conclusion": "success",
            "head_branch": "main",
            "actor": {"login": row["actor"]},
            "repository": {"full_name": "fixture/chummer6-ui"},
        }

    monkeypatch.setattr(MODULE, "fetch_github_api_json", fetch)


def stage_native_fixture(
    stage: Path,
    tuples: dict[tuple[str, str, str], dict],
    root: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    evidence = root / "native-evidence"
    write_native_evidence_source(evidence, stage, tuples)
    archive = root / "native-evidence-finalized.zip"
    zip_evidence(evidence, archive)
    configure_github_api(monkeypatch, archive)
    MODULE.stage_native_evidence(stage, archive)


def write_retained_source(stage: Path, retained_rows: list[dict]) -> dict:
    canonical = {
        "contractName": "Chummer.Hub.Registry.Contracts",
        "version": "incumbent-v1",
        "channelId": "preview",
        "artifacts": retained_rows,
    }
    releases = {
        "contractName": "Chummer.Hub.Registry.Contracts",
        "version": "incumbent-v1",
        "channel": "preview",
        "downloads": [{**row, "channelId": "preview", "version": "incumbent-v1"} for row in retained_rows],
    }
    write_json(stage / "retained-source" / "RELEASE_CHANNEL.generated.json", canonical)
    write_json(stage / "retained-source" / "releases.json", releases)
    retained_names: set[str] = set()
    for row in retained_rows:
        retained_names.add(row["fileName"])
        if row.get("payloadFileName"):
            retained_names.add(row["payloadFileName"])
    files_inventory = [
        {
            "path": name,
            "sha256": sha256(stage / "files" / name),
            "sizeBytes": (stage / "files" / name).stat().st_size,
        }
        for name in sorted(retained_names)
    ]
    return {
        "version": "incumbent-v1",
        "canonicalSha256": sha256(stage / "retained-source" / "RELEASE_CHANNEL.generated.json"),
        "compatibilitySha256": sha256(stage / "retained-source" / "releases.json"),
        "filesInventorySha256": MODULE.inventory_sha256(files_inventory),
        "fileCount": len(files_inventory),
        "files": files_inventory,
    }


def write_seal_receipts(stage: Path, tuples: dict[tuple[str, str, str], dict]) -> None:
    for head in ("avalonia", "blazor-desktop"):
        row = tuples[(head, "windows", "win-x64")]
        write_json(
            stage / f"UI_WINDOWS_DESKTOP_EXIT_GATE-{head}-win-x64.generated.json",
            {
                "contract_name": "chummer6-ui.windows_desktop_exit_gate",
                "status": "passed",
                "channelId": "preview",
                "releaseVersion": "run-fixture-1",
                "head": {"app_key": head, "platform": "windows", "rid": "win-x64"},
                "blockingMode": "none",
                "blocking_mode": "none",
                "reasons": [],
                "checks": {
                    "installer_sha256": row["sha256"],
                    "startup_smoke_artifact_digest": f"sha256:{row['sha256']}",
                    "windows_installer_visual_effective_artifact_digest": row["sha256"],
                    "windows_installer_visual_proof_skipped": False,
                },
            },
        )
        write_json(
            stage / "signing" / f"signing-{head}-win-x64.receipt.json",
            {
                "platform": "windows",
                "app": head,
                "rid": "win-x64",
                "releaseVersion": "run-fixture-1",
                "releaseChannel": "preview",
                "signingStatus": "skipped_preview",
                "artifacts": [
                    {
                        "fileName": row["fileName"],
                        "sha256": row["sha256"],
                        "signingStatus": "skipped_preview",
                    }
                ],
            },
        )
    write_json(
        stage / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json",
        json.loads(
            (stage / "UI_WINDOWS_DESKTOP_EXIT_GATE-avalonia-win-x64.generated.json").read_text()
        ),
    )
    write_json(
        stage / "WINDOWS_BOOTSTRAP_NATIVE_SMOKE.generated.json",
        {
            "status": "pass",
            "errors": [],
            "releaseVersion": "run-fixture-1",
            "releaseChannel": "preview",
            "nativeWindowsRequired": True,
            "checkedArtifacts": [
                {
                    "fileName": tuples[(head, "windows", "win-x64")]["fileName"],
                    "head": head,
                    "rid": "win-x64",
                    "installerMode": "bootstrap",
                    "payloadAcquisitionMode": "download",
                    "executionEnvironment": "native_windows",
                }
                for head in ("avalonia", "blazor-desktop")
            ],
        },
    )
    write_json(
        stage / "WINDOWS_RELEASE_EVIDENCE.generated.json",
        {
            "contractName": "chummer.windows_release_evidence.v1",
            "status": "pass",
            "verdict": "WINDOWS_FLAGSHIP_READY",
            "version": "run-fixture-1",
            "channel": "preview",
            "launchReady": True,
            "supportabilityFloor": "preview_supported",
            "requireAuthenticode": False,
            "requireNativeWindows": True,
            "allowProofOnlyVisualHandoff": False,
            "proofOnlyVisualHandoffPath": "",
            "caveats": [],
            "errors": [],
            "checkedArtifacts": [
                {
                    "artifactId": tuples[(head, "windows", "win-x64")]["artifactId"],
                    "fileName": tuples[(head, "windows", "win-x64")]["fileName"],
                    "head": head,
                    "rid": "win-x64",
                    "sha256": tuples[(head, "windows", "win-x64")]["sha256"],
                    "signingStatus": "pass",
                    "executionEnvironment": "native_windows",
                    "proofOnlyVisualHandoff": False,
                }
                for head in ("avalonia", "blazor-desktop")
            ],
        },
    )
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())
    write_json(
        stage / "RELEASE_BUILD_HANDOFF.generated.json",
        {
            "contract_name": "chummer.release_build_handoff",
            "channel": "preview",
            "version": "run-fixture-1",
            "artifact_count": len(manifest["artifacts"]),
            "handoff_only": True,
            "handoff_scope": "staged_nightly",
            "stable_release_unchanged": True,
            "requires_separate_publish_lane": True,
            "stage_proof_complete": True,
            "promotion_ready": True,
            "missing_required_platforms": [],
            "missing_required_heads": [],
            "blockers": [],
            "artifacts": [
                {
                    "artifact_id": row["artifactId"],
                    "file_name": row["fileName"],
                    "platform": row["platform"],
                    "rid": row["rid"],
                    "version": "run-fixture-1",
                }
                for row in manifest["artifacts"]
            ],
        },
    )
    write_json(
        stage / "release-evidence" / "public-promotion.json",
        {
            "contractName": "chummer.run.desktop_release_publication",
            "artifacts": [
                {
                    "fileName": row["fileName"],
                    "promotionStatus": "pass",
                    "startupSmokeStatus": "pass",
                    "artifactSha256": row["sha256"],
                    "artifactSizeBytes": row["sizeBytes"],
                    "kind": row["kind"],
                }
                for row in manifest["artifacts"]
            ],
        },
    )


def write_inputs_and_candidate(
    stage: Path,
    authorities: list[dict[str, str]],
    retained: dict,
) -> None:
    input_rows: dict[str, dict[str, str]] = {}
    for input_name, _, _, target_name in MODULE.EXACT_PROOF_INPUTS:
        path = stage / "proof" / "inputs" / target_name
        write_json(path, valid_proof_input_payload(input_name))
        input_rows[input_name] = {
            "path": f"proof/inputs/{target_name}",
            "sha256": sha256(path),
        }
    release = {
        "channel": "preview",
        "version": "run-fixture-1",
        "publishedAt": "2026-07-18T12:00:00Z",
    }
    write_json(
        stage / MODULE.INPUT_FILE_NAME,
        {
            "contractName": MODULE.INPUT_CONTRACT_NAME,
            "contractVersion": 1,
            "status": "validated",
            "release": release,
            "authorities": authorities,
            "nativeWindowsEvidenceAuthority": MODULE.native_evidence_authority(
                Path(os.environ["CHUMMER_UI_ROOT"]), authorities
            ),
            "retainedShelf": retained,
            "inputs": input_rows,
        },
    )
    write_json(
        stage / MODULE.CANDIDATE_FILE_NAME,
        {
            "contractName": MODULE.CONTRACT_NAME,
            "contractVersion": 1,
            "status": "awaiting_native_windows_evidence",
            "uploadAuthorized": False,
            "release": release,
            "authorities": authorities,
            "manifestSha256": sha256(stage / "RELEASE_CHANNEL.generated.json"),
        },
    )


def make_valid_seal_stage(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> tuple[Path, Path, dict[tuple[str, str, str], dict]]:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    authorities = MODULE.validate_authorities(presentation_root)
    stage = tmp_path / "candidate"
    tuples = write_current_stage(stage, native_windows=False)
    retained = write_retained_source(stage, [tuples[("avalonia", "windows", "win-x64")]])
    write_inputs_and_candidate(stage, authorities, retained)
    stage_native_fixture(stage, tuples, tmp_path, monkeypatch)
    write_seal_receipts(stage, tuples)
    return presentation_root, stage, tuples


def set_review_gated_manifest(stage: Path) -> None:
    manifest_path = stage / "RELEASE_CHANNEL.generated.json"
    manifest = json.loads(manifest_path.read_text())
    manifest["supportabilityState"] = "review_required"
    manifest["publicTrustMetrics"]["releaseChannel"]["supportabilityState"] = (
        "review_required"
    )
    manifest["registryBoundaryCoverage"]["releaseChannel"].update(
        {"supportabilityState": "review_required", "publicTrustPosture": "blocked"}
    )
    write_json(manifest_path, manifest)
    candidate_path = stage / MODULE.CANDIDATE_FILE_NAME
    candidate = json.loads(candidate_path.read_text())
    candidate["manifestSha256"] = sha256(manifest_path)
    write_json(candidate_path, candidate)


def set_unsigned_windows_preview(
    stage: Path, tuples: dict[tuple[str, str, str], dict]
) -> None:
    path = stage / "WINDOWS_RELEASE_EVIDENCE.generated.json"
    payload = json.loads(path.read_text())
    payload.update(
        {
            "status": "proof_only",
            "verdict": "WINDOWS_PROOF_PREVIEW_READY",
            "launchReady": False,
            "supportabilityFloor": "review_required",
            "caveats": [
                f"{tuples[(head, 'windows', 'win-x64')]['artifactId']}: unsigned preview artifact"
                for head in ("avalonia", "blazor-desktop")
            ],
        }
    )
    for row in payload["checkedArtifacts"]:
        row["signingStatus"] = "skipped_preview"
    write_json(path, payload)


def test_native_evidence_is_api_archive_bound_and_replaces_windows_receipts(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    authorities = MODULE.validate_authorities(presentation_root)
    stage = tmp_path / "candidate"
    tuples = write_current_stage(stage, native_windows=False)
    retained = write_retained_source(stage, [tuples[("avalonia", "windows", "win-x64")]])
    write_inputs_and_candidate(stage, authorities, retained)
    evidence = tmp_path / "native-evidence"
    write_native_evidence_source(evidence, stage, tuples)
    evidence_digest = MODULE.inventory_sha256(MODULE.inventory_tree(evidence))
    archive = tmp_path / "native-evidence-finalized.zip"
    zip_evidence(evidence, archive)
    configure_github_api(monkeypatch, archive)

    payload = MODULE.stage_native_evidence(stage, archive)

    assert payload["status"] == "passed"
    assert payload["treeSha256"] == evidence_digest
    assert payload["archiveSha256"] == sha256(archive)
    assert payload["githubActionsProvenance"]["finalization"]["artifactId"] == 502
    for head in ("avalonia", "blazor-desktop"):
        receipt = json.loads(
            (stage / "startup-smoke" / f"startup-smoke-{head}-win-x64.receipt.json").read_text()
        )
        assert receipt["executionEnvironment"] == "native_windows"
        progress_name = f"screenshots/windows-installer-{head}-win-x64-progress.png"
        assert (stage / "proof" / "windows-native" / progress_name).is_file()
        portable_visual = json.loads(
            (stage / f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json").read_text()
        )
        assert portable_visual["screenshots"][0]["path"] == f"proof/windows-native/{progress_name}"


def test_seal_contract_rejects_retained_noncurrent_tuple_drop(tmp_path: Path) -> None:
    stage = tmp_path / "candidate"
    incoming = write_current_stage(stage, native_windows=True)
    retained_artifact = stage / "files" / "chummer-avalonia-osx-arm64-installer.dmg"
    retained_artifact.write_bytes(b"retained-macos")
    write_json(
        stage / "retained-source" / "RELEASE_CHANNEL.generated.json",
        {
            "version": "incumbent-v1",
            "channelId": "preview",
            "artifacts": [
                {
                    "artifactId": "avalonia-osx-arm64-installer",
                    "head": "avalonia",
                    "platform": "macos",
                    "rid": "osx-arm64",
                    "kind": "installer",
                    "fileName": retained_artifact.name,
                    "sha256": sha256(retained_artifact),
                    "sizeBytes": retained_artifact.stat().st_size,
                }
            ],
        },
    )

    with pytest.raises(MODULE.ContractError, match="dropped retained artifact"):
        MODULE.verify_retained_shelf_preservation(stage, incoming)


def test_seal_inventory_detects_post_seal_byte_drift(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    MODULE.seal_stage(presentation_root, stage)
    assert MODULE.verify_seal(stage)["status"] == "sealed"

    payload_file = stage / "files" / tuples[("avalonia", "linux", "linux-x64")]["fileName"]
    payload_file.write_bytes(b"drift")
    with pytest.raises(MODULE.ContractError, match="digest mismatch|bytes changed"):
        MODULE.verify_seal(stage)


def test_seal_stage_requires_complete_evidence_and_emits_dry_run_only_receipt(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, stage, _ = make_valid_seal_stage(tmp_path, monkeypatch)

    seal = MODULE.seal_stage(presentation_root, stage)

    assert seal["status"] == "sealed"
    assert seal["uploadBoundary"]["producerMode"] == "stage_only"
    assert seal["uploadBoundary"]["uploadAuthorized"] is False
    assert seal["uploadBoundary"]["requiredFirstConsumerMode"] == "dry_run"
    assert seal["uploadBoundary"]["postUploadHandoffEmitted"] is False
    assert seal["uploadBoundary"]["postUploadHandoffContract"] == "chummer.release-upload-handoff/v1"
    assert MODULE.verify_seal(stage)["stage"]["treeSha256"] == seal["stage"]["treeSha256"]


def test_seal_accepts_only_truthfully_review_gated_unsigned_native_preview(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    set_unsigned_windows_preview(stage, tuples)
    set_review_gated_manifest(stage)

    seal = MODULE.seal_stage(presentation_root, stage)

    assert seal["checks"]["windowsReleaseEvidenceTruthfullyBound"] is True
    assert MODULE.verify_seal(stage)["status"] == "sealed"


@pytest.mark.parametrize(
    ("field", "value"),
    (
        ("top", None),
        ("top", "preview_supported"),
        ("public", None),
        ("public", "preview_supported"),
        ("registry", None),
        ("registry", "preview_supported"),
        ("posture", None),
        ("posture", "preview"),
    ),
)
def test_unsigned_native_preview_rejects_missing_or_optimistic_canonical_posture(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    field: str,
    value: str | None,
) -> None:
    presentation_root, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    set_unsigned_windows_preview(stage, tuples)
    set_review_gated_manifest(stage)
    manifest_path = stage / "RELEASE_CHANNEL.generated.json"
    manifest = json.loads(manifest_path.read_text())
    if field == "top":
        target, key = manifest, "supportabilityState"
    elif field == "public":
        target, key = manifest["publicTrustMetrics"]["releaseChannel"], "supportabilityState"
    elif field == "registry":
        target, key = manifest["registryBoundaryCoverage"]["releaseChannel"], "supportabilityState"
    else:
        target, key = manifest["registryBoundaryCoverage"]["releaseChannel"], "publicTrustPosture"
    if value is None:
        target.pop(key)
    else:
        target[key] = value
    write_json(manifest_path, manifest)
    candidate_path = stage / MODULE.CANDIDATE_FILE_NAME
    candidate = json.loads(candidate_path.read_text())
    candidate["manifestSha256"] = sha256(manifest_path)
    write_json(candidate_path, candidate)

    with pytest.raises(
        MODULE.ContractError,
        match="review_required canonical supportability|pinned Registry projection",
    ):
        MODULE.seal_stage(presentation_root, stage)


def test_authority_validation_rejects_swapped_repository_roles(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    core_root = os.environ["CHUMMER_CORE_ROOT"]
    core_commit = os.environ["CHUMMER_CORE_EXPECTED_COMMIT"]
    run_root = os.environ["CHUMMER_RUN_ROOT"]
    run_commit = os.environ["CHUMMER_RUN_EXPECTED_COMMIT"]
    monkeypatch.setenv("CHUMMER_CORE_ROOT", run_root)
    monkeypatch.setenv("CHUMMER_CORE_EXPECTED_COMMIT", run_commit)
    monkeypatch.setenv("CHUMMER_RUN_ROOT", core_root)
    monkeypatch.setenv("CHUMMER_RUN_EXPECTED_COMMIT", core_commit)

    with pytest.raises(
        MODULE.ContractError,
        match="repository identity sentinel is missing|compatibility-tree path consumed by the build",
    ):
        MODULE.validate_authorities(presentation_root)


def test_releases_json_digest_drift_is_rejected(tmp_path: Path) -> None:
    stage = tmp_path / "candidate"
    write_current_stage(stage, native_windows=False)
    compatibility = json.loads((stage / "releases.json").read_text())
    compatibility["downloads"][0]["sha256"] = "f" * 64

    with pytest.raises(MODULE.ContractError, match="digest differs"):
        MODULE.verify_compatibility_manifest(
            json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text()),
            compatibility,
            stage / "files",
        )


def test_current_tuple_wrong_kind_is_rejected(tmp_path: Path) -> None:
    stage = tmp_path / "candidate"
    write_current_stage(stage, native_windows=False)
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())
    manifest["artifacts"][0]["kind"] = "portable"
    write_json(stage / "RELEASE_CHANNEL.generated.json", manifest)

    with pytest.raises(MODULE.ContractError, match="missing current nightly tuples|not an installer"):
        MODULE.require_current_artifacts(stage)


@pytest.mark.parametrize(
    ("field", "value", "message"),
    (
        ("bootstrapPayloadAcquisitionMode", "embedded", "download acquisition"),
        ("bootstrapPayloadSha256", "f" * 64, "payload digest mismatch"),
        ("platform", "linux", "platform mismatch"),
        ("version", "wrong-release", "version mismatch"),
        ("bootstrapPayloadSizeBytes", 999999, "payload size mismatch"),
    ),
)
def test_windows_download_smoke_forgery_is_rejected(
    tmp_path: Path, field: str, value: object, message: str
) -> None:
    stage = tmp_path / "candidate"
    tuples = write_current_stage(stage, native_windows=True)
    path = stage / "startup-smoke" / "startup-smoke-avalonia-win-x64.receipt.json"
    receipt = json.loads(path.read_text())
    receipt[field] = value
    write_json(path, receipt)

    with pytest.raises(MODULE.ContractError, match=message):
        MODULE.verify_current_startup_receipts(stage, tuples, require_native_windows=True)


@pytest.mark.parametrize("mutation", ("wrong_contract", "duplicate_roles", "wrong_release", "wrong_digest"))
def test_native_visual_proof_forgery_is_rejected(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, mutation: str
) -> None:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    authorities = MODULE.validate_authorities(presentation_root)
    stage = tmp_path / "candidate"
    tuples = write_current_stage(stage, native_windows=False)
    retained = write_retained_source(stage, [tuples[("avalonia", "windows", "win-x64")]])
    write_inputs_and_candidate(stage, authorities, retained)
    evidence = tmp_path / "native-evidence"
    write_native_evidence_source(evidence, stage, tuples)
    path = evidence / "WINDOWS_INSTALLER_VISUAL_PROOF-avalonia-win-x64.generated.json"
    proof = json.loads(path.read_text())
    if mutation == "wrong_contract":
        proof["contractName"] = "forged.contract"
    elif mutation == "duplicate_roles":
        proof["screenshots"][1]["role"] = "progress"
    elif mutation == "wrong_release":
        proof["version"] = "run-forged"
    else:
        proof["artifactDigest"] = "sha256:" + "f" * 64
    write_json(path, proof)
    archive = tmp_path / "native-evidence-finalized.zip"
    zip_evidence(evidence, archive)
    configure_github_api(monkeypatch, archive)

    with pytest.raises(MODULE.ContractError):
        MODULE.stage_native_evidence(stage, archive)
    assert not (stage / "proof" / "windows-native").exists()


def test_native_visual_reviewer_cannot_self_authorize(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    authorities = MODULE.validate_authorities(presentation_root)
    stage = tmp_path / "candidate"
    tuples = write_current_stage(stage, native_windows=False)
    retained = write_retained_source(stage, [tuples[("avalonia", "windows", "win-x64")]])
    write_inputs_and_candidate(stage, authorities, retained)
    evidence = tmp_path / "native-evidence"
    write_native_evidence_source(evidence, stage, tuples, reviewer="capture-user")
    archive = tmp_path / "native-evidence-finalized.zip"
    zip_evidence(evidence, archive)
    configure_github_api(monkeypatch, archive)

    with pytest.raises(MODULE.ContractError, match="self-reviewing"):
        MODULE.stage_native_evidence(stage, archive)


def test_native_evidence_archive_must_match_github_api_digest(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    authorities = MODULE.validate_authorities(presentation_root)
    stage = tmp_path / "candidate"
    tuples = write_current_stage(stage, native_windows=False)
    retained = write_retained_source(stage, [tuples[("avalonia", "windows", "win-x64")]])
    write_inputs_and_candidate(stage, authorities, retained)
    evidence = tmp_path / "native-evidence"
    write_native_evidence_source(evidence, stage, tuples)
    archive = tmp_path / "native-evidence-finalized.zip"
    zip_evidence(evidence, archive)
    configure_github_api(monkeypatch, archive)
    archive.write_bytes(archive.read_bytes() + b"post-api-digest-mutation")

    with pytest.raises(MODULE.ContractError, match="GitHub artifact digest"):
        MODULE.stage_native_evidence(stage, archive)
    assert not (stage / "proof" / "windows-native").exists()


@pytest.mark.parametrize("mutation", ("workflow", "actor", "event", "expired", "artifact_id"))
def test_github_actions_api_provenance_fails_closed(
    monkeypatch: pytest.MonkeyPatch, mutation: str
) -> None:
    source = {
        "repository": "fixture/chummer6-ui",
        "workflow": MODULE.NATIVE_CAPTURE_WORKFLOW,
        "runId": "1001",
        "runAttempt": "1",
        "ref": "refs/heads/main",
        "sha": "a" * 40,
        "actor": "capture-user",
        "artifactName": "windows-native-evidence-1001-1",
    }
    run = {
        "id": 1001,
        "path": source["workflow"],
        "head_sha": source["sha"],
        "run_attempt": 1,
        "event": "workflow_dispatch",
        "status": "completed",
        "conclusion": "success",
        "head_branch": "main",
        "actor": {"login": source["actor"]},
        "repository": {"full_name": source["repository"]},
    }
    artifact = {
        "id": 501,
        "name": source["artifactName"],
        "expired": False,
        "digest": "sha256:" + "b" * 64,
        "workflow_run": {"id": 1001, "head_sha": source["sha"]},
    }
    if mutation == "workflow":
        run["path"] = ".github/workflows/forged.yml"
    elif mutation == "actor":
        run["actor"] = {"login": "forged-user"}
    elif mutation == "event":
        run["event"] = "push"
    elif mutation == "expired":
        artifact["expired"] = True
    else:
        artifact["id"] = 0
    monkeypatch.setattr(
        MODULE,
        "fetch_github_api_json",
        lambda url: {"total_count": 1, "artifacts": [artifact]}
        if url.endswith("/artifacts?per_page=100")
        else run,
    )

    with pytest.raises(MODULE.ContractError, match="GitHub Actions"):
        MODULE.verify_github_actions_provenance(source)


def test_retained_noncurrent_digest_drift_is_rejected(tmp_path: Path) -> None:
    stage = tmp_path / "candidate"
    incoming = write_current_stage(stage, native_windows=True)
    retained_file = stage / "files" / "chummer-avalonia-osx-arm64-installer.dmg"
    retained_file.write_bytes(b"retained")
    retained_row = {
        "artifactId": "avalonia-osx-arm64-installer",
        "head": "avalonia",
        "platform": "macos",
        "rid": "osx-arm64",
        "kind": "installer",
        "fileName": retained_file.name,
        "sha256": sha256(retained_file),
        "sizeBytes": retained_file.stat().st_size,
    }
    write_retained_source(stage, [retained_row])
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())
    manifest["artifacts"].append({**retained_row, "sha256": "f" * 64})
    write_json(stage / "RELEASE_CHANNEL.generated.json", manifest)

    with pytest.raises(MODULE.ContractError, match="changed retained non-current tuple bytes"):
        MODULE.verify_retained_shelf_preservation(stage, incoming)


@pytest.mark.parametrize("mutation", ("remove", "mutate", "receipt_digest", "receipt_count"))
def test_retained_inventory_binds_auxiliary_files_and_summary(
    tmp_path: Path, mutation: str
) -> None:
    stage = tmp_path / "candidate"
    write_current_stage(stage, native_windows=True)
    retained_file = stage / "files" / "retained-shelf-metadata.json"
    retained_file.write_text('{"incumbent":true}\n', encoding="utf-8")
    retained = write_retained_source(stage, [])
    retained["files"] = [
        {
            "path": retained_file.name,
            "sha256": sha256(retained_file),
            "sizeBytes": retained_file.stat().st_size,
        }
    ]
    retained["fileCount"] = 1
    retained["filesInventorySha256"] = MODULE.inventory_sha256(retained["files"])
    retained_manifest = json.loads(
        (stage / "retained-source" / "RELEASE_CHANNEL.generated.json").read_text()
    )
    MODULE.verify_retained_files_inventory(stage, retained, retained_manifest)

    if mutation == "remove":
        retained_file.unlink()
    elif mutation == "mutate":
        retained_file.write_text('{"incumbent":false}\n', encoding="utf-8")
    elif mutation == "receipt_digest":
        retained["filesInventorySha256"] = "f" * 64
    else:
        retained["fileCount"] = 2

    with pytest.raises(MODULE.ContractError, match="retained"):
        MODULE.verify_retained_files_inventory(stage, retained, retained_manifest)


def test_per_head_exit_gate_set_is_required(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    (stage / "UI_WINDOWS_DESKTOP_EXIT_GATE-blazor-desktop-win-x64.generated.json").unlink()

    with pytest.raises(MODULE.ContractError):
        MODULE.verify_windows_exit_gates(
            stage,
            json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text()),
            tuples,
        )


def test_per_head_exit_gate_wrong_contract_is_rejected(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    _, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    path = stage / "UI_WINDOWS_DESKTOP_EXIT_GATE-avalonia-win-x64.generated.json"
    gate = json.loads(path.read_text())
    gate["contract_name"] = "forged.exit-gate"
    write_json(path, gate)

    with pytest.raises(MODULE.ContractError, match="wrong contract"):
        MODULE.verify_windows_exit_gates(
            stage,
            json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text()),
            tuples,
        )


def test_seal_semantic_mutation_is_rejected_even_when_seal_inventory_is_unchanged(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, stage, _ = make_valid_seal_stage(tmp_path, monkeypatch)
    MODULE.seal_stage(presentation_root, stage)
    seal_path = stage / MODULE.SEAL_FILE_NAME
    seal = json.loads(seal_path.read_text())
    seal["release"]["version"] = "forged-release"
    write_json(seal_path, seal)

    with pytest.raises(MODULE.ContractError, match="semantic field changed: release"):
        MODULE.verify_seal(stage)


def test_forged_gate_is_rejected_even_after_recomputing_seal_inventory(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, stage, _ = make_valid_seal_stage(tmp_path, monkeypatch)
    MODULE.seal_stage(presentation_root, stage)
    gate_name = "UI_WINDOWS_DESKTOP_EXIT_GATE-avalonia-win-x64.generated.json"
    path = stage / gate_name
    gate = json.loads(path.read_text())
    gate["status"] = "passed"
    gate["releaseVersion"] = "forged-release"
    write_json(path, gate)
    seal_path = stage / MODULE.SEAL_FILE_NAME
    seal = json.loads(seal_path.read_text())
    inventory = MODULE.inventory_tree(stage, exclusions=(MODULE.SEAL_FILE_NAME,))
    seal["stage"] = {
        "files": inventory,
        "fileCount": len(inventory),
        "treeSha256": MODULE.inventory_sha256(inventory),
    }
    write_json(seal_path, seal)

    with pytest.raises(
        MODULE.ContractError,
        match="authoritative validation receipt exit-gate binding differs|release identity differs",
    ):
        MODULE.verify_seal(stage)


def test_run_upload_candidate_matches_real_run_summarizer(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, stage, _ = make_valid_seal_stage(tmp_path, monkeypatch)
    (stage / "signing").mkdir(exist_ok=True)
    (stage / "signing" / "private-seal-only.json").write_text("{}\n")
    MODULE.seal_stage(presentation_root, stage)
    helper = Path(
        os.environ.get(
            "CHUMMER_RUN_UPLOAD_ATTEMPT_RECEIPT_HELPER",
            str(
                REPO_ROOT.parent
                / "chummer.run-services"
                / "scripts"
                / "release"
                / "release_upload_attempt_receipt.py"
            ),
        )
    )
    if not helper.is_file():
        pytest.skip("real hardened Run upload-attempt helper is unavailable")
    output = tmp_path / "run-candidate.json"
    command = [
        sys.executable,
        str(helper),
        "summarize",
        "--bundle-root",
        str(stage),
        "--canonical-manifest",
        str(stage / "RELEASE_CHANNEL.generated.json"),
        "--output",
        str(output),
    ]
    hosted_paths = [
        stage / "releases.json",
        stage / "RELEASE_CHANNEL.generated.json",
        stage / "release-evidence" / "public-promotion.json",
    ]
    for directory_name in ("files", "startup-smoke"):
        hosted_paths.extend(
            sorted(
                path
                for path in (stage / directory_name).rglob("*")
                if path.is_file()
            )
        )
    hosted_paths = sorted(hosted_paths, key=lambda path: path.relative_to(stage).as_posix())
    assert MODULE.HOSTED_BOOTSTRAP_SHA256 == (
        "74e5e19e7622cadf46880e140eff385d16ed136d200494f63529f4f01b7935fd"
    )
    assert [path.relative_to(stage).as_posix() for path in MODULE.run_upload_inventory_paths(stage)] == [
        path.relative_to(stage).as_posix() for path in hosted_paths
    ]
    assert all("/proof/" not in f"/{path.relative_to(stage).as_posix()}" for path in hosted_paths)
    assert all("/signing/" not in f"/{path.relative_to(stage).as_posix()}" for path in hosted_paths)
    for path in hosted_paths:
        command.extend(("--file", str(path)))
    subprocess.run(command, check=True)

    assert json.loads(output.read_text()) == json.loads(
        (stage / MODULE.RUN_UPLOAD_CANDIDATE_FILE_NAME).read_text()
    )


def test_atomic_no_replace_install_preserves_concurrent_destination(tmp_path: Path) -> None:
    source = tmp_path / "source"
    destination = tmp_path / "destination"
    source.mkdir()
    destination.mkdir()
    (source / "owned.txt").write_text("owned\n")
    (destination / "concurrent.txt").write_text("concurrent\n")
    identity = MODULE.directory_identity(source)

    with pytest.raises(MODULE.ContractError, match="destination already exists"):
        MODULE.atomic_install_directory_no_replace(
            source,
            destination,
            expected_device=identity["device"],
            expected_inode=identity["inode"],
        )

    assert (source / "owned.txt").read_text() == "owned\n"
    assert (destination / "concurrent.txt").read_text() == "concurrent\n"


def test_atomic_install_and_owned_candidate_tombstone_are_identity_bound(tmp_path: Path) -> None:
    source = tmp_path / "source"
    destination = tmp_path / "destination"
    source.mkdir()
    (source / "owned.txt").write_text("owned\n")
    identity = MODULE.directory_identity(source)

    assert MODULE.atomic_install_directory_no_replace(
        source,
        destination,
        expected_device=identity["device"],
        expected_inode=identity["inode"],
    ) == identity
    assert not source.exists()
    assert (destination / "owned.txt").read_text() == "owned\n"

    candidate = tmp_path / "candidate"
    candidate.mkdir()
    candidate_identity = MODULE.directory_identity(candidate)
    with pytest.raises(MODULE.ContractError, match="identity changed"):
        MODULE.consume_owned_directory(
            candidate,
            tmp_path / "wrong-cleanup",
            expected_device=candidate_identity["device"],
            expected_inode=candidate_identity["inode"] + 1,
        )
    assert candidate.is_dir()
    MODULE.consume_owned_directory(
        candidate,
        tmp_path / "cleanup",
        expected_device=candidate_identity["device"],
        expected_inode=candidate_identity["inode"],
    )
    assert not candidate.exists()
    assert not (tmp_path / "cleanup").exists()


def test_atomic_install_rejects_source_replaced_after_identity_capture(tmp_path: Path) -> None:
    source = tmp_path / "source"
    displaced = tmp_path / "displaced"
    destination = tmp_path / "destination"
    source.mkdir()
    expected = MODULE.directory_identity(source)
    source.rename(displaced)
    source.mkdir()

    with pytest.raises(MODULE.ContractError, match="source identity changed"):
        MODULE.atomic_install_directory_no_replace(
            source,
            destination,
            expected_device=expected["device"],
            expected_inode=expected["inode"],
        )

    assert source.is_dir()
    assert displaced.is_dir()
    assert not destination.exists()


def test_verified_install_rejects_boundary_mutation_and_removes_installed_tree(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, source, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    MODULE.seal_stage(presentation_root, source)
    destination = tmp_path / "installed-stage"
    identity = MODULE.directory_identity(source)
    expected_tree = MODULE.digest_tree(
        source,
        expected_device=identity["device"],
        expected_inode=identity["inode"],
    )["treeSha256"]
    artifact_name = tuples[("avalonia", "linux", "linux-x64")]["fileName"]
    original_rename = MODULE._renameat2_no_replace
    mutated = False

    def rename_then_mutate(
        source_parent_fd: int,
        source_name: str,
        destination_parent_fd: int,
        destination_name: str,
    ) -> int:
        nonlocal mutated
        result = original_rename(
            source_parent_fd,
            source_name,
            destination_parent_fd,
            destination_name,
        )
        if (
            result == 0
            and not mutated
            and source_name == source.name
            and destination_name == destination.name
        ):
            (destination / "files" / artifact_name).write_bytes(b"boundary mutation")
            mutated = True
        return result

    monkeypatch.setattr(MODULE, "_renameat2_no_replace", rename_then_mutate)
    with pytest.raises(MODULE.ContractError, match="boundary validation and was removed"):
        MODULE.install_verified_sealed_directory_no_replace(
            source,
            destination,
            expected_device=identity["device"],
            expected_inode=identity["inode"],
            expected_tree_sha256=expected_tree,
        )

    assert mutated is True
    assert not destination.exists()
    assert not source.exists()


def test_verified_install_removes_installed_tree_after_unicode_validation_error(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, source, _ = make_valid_seal_stage(tmp_path, monkeypatch)
    MODULE.seal_stage(presentation_root, source)
    destination = tmp_path / "installed-stage"
    identity = MODULE.directory_identity(source)
    expected_tree = MODULE.digest_tree(
        source,
        expected_device=identity["device"],
        expected_inode=identity["inode"],
    )["treeSha256"]
    original_digest_tree = MODULE.digest_tree
    corrupted = False

    def digest_then_corrupt_seal(
        root: Path,
        *,
        expected_device: int | None = None,
        expected_inode: int | None = None,
    ) -> dict:
        nonlocal corrupted
        result = original_digest_tree(
            root,
            expected_device=expected_device,
            expected_inode=expected_inode,
        )
        if root == destination and not corrupted:
            (destination / MODULE.SEAL_FILE_NAME).write_bytes(b"\xff")
            corrupted = True
        return result

    monkeypatch.setattr(MODULE, "digest_tree", digest_then_corrupt_seal)
    with pytest.raises(MODULE.ContractError, match="utf-8"):
        MODULE.install_verified_sealed_directory_no_replace(
            source,
            destination,
            expected_device=identity["device"],
            expected_inode=identity["inode"],
            expected_tree_sha256=expected_tree,
        )

    assert corrupted is True
    assert not destination.exists()
    assert not source.exists()


def test_authoritative_replay_rejects_minimal_self_authorized_proof(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, stage, _ = make_valid_seal_stage(tmp_path, monkeypatch)
    proof_path = stage / "proof" / "inputs" / "HUB_LOCAL_RELEASE_PROOF.generated.json"
    write_json(proof_path, {"status": "passed"})
    inputs_path = stage / MODULE.INPUT_FILE_NAME
    inputs = json.loads(inputs_path.read_text())
    inputs["inputs"]["hubLocalReleaseProof"]["sha256"] = sha256(proof_path)
    write_json(inputs_path, inputs)

    with pytest.raises(MODULE.ContractError, match="Registry authoritative proof validation"):
        MODULE.seal_stage(presentation_root, stage)


def test_authoritative_validator_bytes_are_bound_to_exact_commit(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    authorities = MODULE.validate_authorities(presentation_root)
    presentation_commit = next(
        row["commit"] for row in authorities if row["name"] == "presentation"
    )
    relative = "scripts/verify-windows-release-evidence.py"
    binding = MODULE.require_committed_authority_file(
        presentation_root,
        presentation_commit,
        relative,
        "fixture validator",
    )
    assert binding["sha256"] == MODULE.committed_file_sha256(
        presentation_root, presentation_commit, relative
    )

    validator = presentation_root / relative
    validator.write_bytes(validator.read_bytes() + b"\n# drift after authority check\n")
    with pytest.raises(MODULE.ContractError, match="worktree bytes differ from git"):
        MODULE.require_committed_authority_file(
            presentation_root,
            presentation_commit,
            relative,
            "fixture validator",
        )


def test_authoritative_replay_rejects_minimal_receipts_for_every_upstream_gate(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    authorities = MODULE.validate_authorities(presentation_root)
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())
    target_by_name = {row[0]: row[3] for row in MODULE.EXACT_PROOF_INPUTS}
    for input_name in (
        "uiLocalizationReleaseGate",
        "uiLocalReleaseProof",
        "blazorSelfHostWorkbenchProof",
        "blazorPublicEdgeWorkbenchProof",
        "blazorBrowserLaneProofSet",
        "uiFlagshipReleaseGate",
        "desktopWorkflowExecutionGate",
            "uiWorkflowParity",
            "sr4WorkflowParity",
            "sr6WorkflowParity",
        ):
        path = stage / "proof" / "inputs" / target_by_name[input_name]
        original = path.read_bytes()
        write_json(path, {"status": "passed", "generatedAt": "2026-07-18T12:00:00Z"})
        try:
            try:
                MODULE.replay_authoritative_stage_validators(
                    presentation_root,
                    stage,
                    manifest,
                    tuples,
                    authorities,
                )
            except MODULE.ContractError:
                pass
            else:
                pytest.fail(f"minimal upstream receipt unexpectedly passed: {input_name}")
        finally:
            path.write_bytes(original)


def test_orchestrator_is_stage_only_and_requires_all_current_tuple_gates() -> None:
    source = ORCHESTRATOR_PATH.read_text(encoding="utf-8")
    assert "CHUMMER_RELEASE_MANIFEST_STAGE_ONLY=1" in source
    assert "CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS=1" in source
    assert "CHUMMER_WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE=download" in source
    assert "--require-native-windows" in source
    assert "verify_release_shelf_replacement.py" in source
    assert "PREVIEW_NIGHTLY_NATIVE_WINDOWS_EVIDENCE" in source
    assert "unset CHUMMER_FORCE_NIGHTLY_PUBLISH" in source
    assert "unset CHUMMER_PUBLISHED_FEED_SOURCES" in source
    assert 'NUGET_PACKAGES="$CANDIDATE_DIR/work/nuget-packages"' in source
    assert "invalidate_reference_assembly_caches" in source
    assert "acquire_package_plane_lock" in source
    assert "CHUMMER_PACKAGE_PLANE_LOCK_HELD=1" in source
    assert "CHUMMER_PREVIEW_NIGHTLY_NATIVE_WINDOWS_EVIDENCE_ARCHIVE" in source
    assert "authenticated upstream visual reviewer" in source
    assert 'CANDIDATE_DIR="$sealing_work"' in source
    assert "candidate changed while creating transactional seal copy" in source
    assert "install-verified-sealed-dir-no-replace" in source
    assert "--expected-tree-sha256" in source
    assert '--expected-device "$sealing_work_device"' in source
    assert '--expected-inode "$sealing_work_inode"' in source
    assert 'rm -rf -- "$CANDIDATE_DIR"' not in source
    assert 'mv -- "$CANDIDATE_DIR" "$STAGE_DIR"' not in source
    assert source.count('--windows-exit-gate "$CANDIDATE_DIR/UI_WINDOWS_DESKTOP_EXIT_GATE-') == 2
    assert "--allow-proof-only-visual-handoff" not in source
    assert "curl " not in source
    assert "upload-sessions" not in source
    assert "publish-latest-nightly-to-downloads" not in source
    assert "RELEASE_UPLOAD_TICKET" not in source
    for invocation in (
        "publish_project avalonia \"$REPO_ROOT/Chummer.Avalonia/Chummer.Avalonia.csproj\" win-x64",
        "publish_project avalonia \"$REPO_ROOT/Chummer.Avalonia/Chummer.Avalonia.csproj\" linux-x64",
        "publish_project blazor-desktop \"$REPO_ROOT/Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj\" win-x64",
        "publish_project blazor-desktop \"$REPO_ROOT/Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj\" linux-x64",
    ):
        assert invocation in source


def test_manifest_generator_has_fail_closed_stage_only_secondary_sync_boundary() -> None:
    source = MANIFEST_GENERATOR_PATH.read_text(encoding="utf-8")
    assert 'MANIFEST_STAGE_ONLY="${CHUMMER_RELEASE_MANIFEST_STAGE_ONLY:-0}"' in source
    assert 'if ! to_bool "$MANIFEST_STAGE_ONLY"; then\n  sync_portal_outputs' in source
    assert (
        "stage-only manifest generation skipped portal, run-services, presentation, and registry publication sync"
        in source
    )
