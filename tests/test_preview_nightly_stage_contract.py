from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import binascii
import shutil
import stat
import struct
import subprocess
import sys
import tempfile
import warnings
import zipfile
import zlib
from datetime import UTC, datetime, timedelta
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
HELPER_PATH = REPO_ROOT / "scripts" / "preview_nightly_stage_contract.py"
ORCHESTRATOR_PATH = REPO_ROOT / "scripts" / "build-preview-nightly-stage.sh"
MANIFEST_GENERATOR_PATH = REPO_ROOT / "scripts" / "generate-releases-manifest.sh"
MANIFEST_VERIFIER_PATH = REPO_ROOT / "scripts" / "verify-releases-manifest.sh"


def load_helper():
    spec = importlib.util.spec_from_file_location("preview_nightly_stage_contract", HELPER_PATH)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


MODULE = load_helper()


def load_supply_chain_fixture_module():
    path = REPO_ROOT / "tests" / "preview_supply_chain_fixtures.py"
    spec = importlib.util.spec_from_file_location("preview_supply_chain_fixtures_stage", path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


SUPPLY_FIXTURES = load_supply_chain_fixture_module()


def load_test_fixture_module(file_name: str, module_name: str):
    path = REPO_ROOT / "tests" / file_name
    spec = importlib.util.spec_from_file_location(module_name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def test_manifest_verifier_forwards_flags_and_positional_target(tmp_path: Path) -> None:
    registry_root = tmp_path / "registry"
    registry_script = registry_root / "scripts" / "verify_public_release_channel.py"
    registry_script.parent.mkdir(parents=True)
    registry_script.write_text(
        "import json, os, sys\n"
        "from pathlib import Path\n"
        "Path(os.environ['VERIFY_ARGS_LOG']).write_text(json.dumps(sys.argv[1:]), encoding='utf-8')\n",
        encoding="utf-8",
    )
    manifest = tmp_path / "RELEASE_CHANNEL.generated.json"
    manifest.write_text("{}\n", encoding="utf-8")
    args_log = tmp_path / "args.json"
    environment = os.environ.copy()
    environment.update(
        {
            "CHUMMER_HUB_REGISTRY_ROOT": str(registry_root),
            "CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE": "0",
            "CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER": "0",
            "CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER": "false",
            "VERIFY_ARGS_LOG": str(args_log),
        }
    )

    subprocess.run(
        [
            "bash",
            str(MANIFEST_VERIFIER_PATH),
            "--require-complete-desktop-coverage",
            "--skip-startup-smoke-filter",
            str(manifest),
        ],
        check=True,
        cwd=REPO_ROOT,
        env=environment,
    )

    assert json.loads(args_log.read_text(encoding="utf-8")) == [
        "--require-complete-desktop-coverage",
        "--skip-startup-smoke-filter",
        str(manifest),
    ]


def registry_test_root() -> Path:
    # Script source only. Do not read CHUMMER_HUB_REGISTRY_ROOT: seal tests bind
    # that env to a temporary authority checkout, and importing from there dirties it.
    configured = os.environ.get("CHUMMER_UI_TEST_REGISTRY_ROOT", "").strip()
    candidates: list[Path] = []
    if configured:
        candidates.append(Path(configured))
    candidates.extend(
        (
            REPO_ROOT.parent / "chummer-hub-registry",
            REPO_ROOT.parent.parent / "chummer-hub-registry",
            Path("/docker/chummercomplete/chummer-hub-registry"),
        )
    )
    for root in candidates:
        if (root / "scripts" / "materialize_public_release_channel.py").is_file():
            return root
    raise FileNotFoundError(
        "Registry test authority is missing. Set CHUMMER_UI_TEST_REGISTRY_ROOT "
        "to a checkout that contains scripts/materialize_public_release_channel.py."
    )


def load_registry_fixture_module():
    registry_path = registry_test_root() / "scripts" / "materialize_public_release_channel.py"
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
                "preview_supply_chain.py",
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
            (workflow_root / "preview-nightly-candidate-export.yml").write_text(
                "name: fixture candidate export\n", encoding="utf-8"
            )
        elif name == "registry":
            (repo / "scripts").mkdir(parents=True, exist_ok=True)
            for script_name in (
                "materialize_public_release_channel.py",
                "verify_public_release_channel.py",
            ):
                shutil.copy2(
                    registry_test_root() / "scripts" / script_name,
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
    copied = candidate / "retained-source" / "files" / retained_artifact.name
    assert copied.read_bytes() == retained_artifact.read_bytes()
    assert list((candidate / "files").iterdir()) == []
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

    assert not (candidate / "retained-source" / "files").exists()


def write_current_stage(
    stage: Path,
    *,
    native_windows: bool,
    source_commit: str | None = None,
) -> dict[tuple[str, str, str], dict]:
    source_commit = source_commit or os.environ.get("CHUMMER_UI_EXPECTED_COMMIT") or "a" * 40
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
        "desktopTupleCoverage": {
            "requiredDesktopHeads": list(MODULE.PROMOTED_WINDOWS_HEADS),
            "requiredDesktopPlatforms": list(
                MODULE.ACTIVE_PREVIEW_DESKTOP_PLATFORMS
            ),
        },
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
    SUPPLY_FIXTURES.write_valid_supply_chain(
        stage,
        version="run-fixture-1",
        source_commit=source_commit,
        supply=MODULE.SUPPLY_CHAIN,
        require_artifact_bytes=True,
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
        "triggeringActor": "capture-user",
        "rerunPolicy": "same-actor-only",
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
        "triggeringActor": reviewer,
        "rerunPolicy": "same-actor-only",
        "artifactName": "windows-native-evidence-finalized-2002-1",
    }
    capture_heads: list[dict] = []
    for head in MODULE.PROMOTED_WINDOWS_HEADS:
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
    producer_source = {
        "repository": "fixture/chummer6-ui",
        "workflow": MODULE.CANDIDATE_EXPORT_WORKFLOW,
        "runId": "900",
        "runAttempt": "1",
        "ref": MODULE.CANDIDATE_EXPORT_REF,
        "sha": os.environ["CHUMMER_UI_EXPECTED_COMMIT"],
        "actor": "producer-user",
        "artifactName": "preview-nightly-candidate-900-1",
        "runnerLabel": "chummer-preview-nightly-export-fixturenonce1",
    }
    candidate_rows = MODULE._candidate_local_content_rows(stage)
    candidate_provenance = evidence / MODULE.CANDIDATE_PROVENANCE_DIRECTORY
    candidate_provenance.mkdir()
    local_supply_chain = MODULE.SUPPLY_CHAIN.content_bindings(stage)
    for category in ("sboms", "scans"):
        for binding in local_supply_chain[category]:
            target = candidate_provenance / binding["path"]
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(stage / binding["path"], target)
    gate_binding = local_supply_chain["gate"]
    gate_target = candidate_provenance / gate_binding["path"]
    gate_target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(stage / gate_binding["path"], gate_target)
    copied_supply_chain = MODULE._prefixed_candidate_supply_chain_bindings(
        local_supply_chain
    )
    content_inventory_path = candidate_provenance / MODULE.CANDIDATE_CONTENT_INVENTORY_FILE_NAME
    write_json(
        content_inventory_path,
        {
            "contractName": MODULE.CANDIDATE_CONTENT_INVENTORY_CONTRACT_NAME,
            "contractVersion": 1,
            "release": {"channel": "preview", "version": "run-fixture-1"},
            "manifest": {
                "path": MODULE.CANDIDATE_MANIFEST_PATH,
                "sha256": sha256(stage / MODULE.CANDIDATE_MANIFEST_PATH),
            },
            "files": candidate_rows,
        },
    )
    export_receipt_path = candidate_provenance / MODULE.CANDIDATE_EXPORT_FILE_NAME
    write_json(
        export_receipt_path,
        {
            "contractName": MODULE.CANDIDATE_EXPORT_CONTRACT_NAME,
            "contractVersion": 1,
            "status": "exported",
            "release": {"channel": "preview", "version": "run-fixture-1"},
            "source": producer_source,
            "candidateManifest": {
                "path": MODULE.CANDIDATE_MANIFEST_PATH,
                "sha256": sha256(stage / MODULE.CANDIDATE_MANIFEST_PATH),
            },
            "contentInventory": {
                "path": MODULE.CANDIDATE_CONTENT_INVENTORY_FILE_NAME,
                "sha256": sha256(content_inventory_path),
            },
            "heads": [
                {
                    "headId": head,
                    "rid": "win-x64",
                    "installer": {
                        "relativePath": f"files/chummer-{head}-win-x64-installer.exe",
                        "fileName": tuples[(head, "windows", "win-x64")]["fileName"],
                        "sha256": tuples[(head, "windows", "win-x64")]["sha256"],
                        "sizeBytes": tuples[(head, "windows", "win-x64")]["sizeBytes"],
                    },
                    "payload": {
                        "relativePath": f"files/chummer-{head}-win-x64-payload.zip",
                        "fileName": tuples[(head, "windows", "win-x64")]["payloadFileName"],
                        "sha256": tuples[(head, "windows", "win-x64")]["payloadSha256"],
                        "sizeBytes": tuples[(head, "windows", "win-x64")]["payloadSizeBytes"],
                    },
                }
                for head in MODULE.PROMOTED_WINDOWS_HEADS
            ],
            "supplyChain": local_supply_chain,
            "supplyChainVerification": {
                "mode": MODULE.SUPPLY_CHAIN.LIVE_VERIFICATION_MODE,
                "releaseAuthoritative": True,
            },
        },
    )
    producer_common = {
        key: producer_source[key]
        for key in (
            "repository",
            "workflow",
            "runId",
            "runAttempt",
            "ref",
            "sha",
            "actor",
            "artifactName",
        )
    }
    artifact_id = "503"
    artifact_sha = "d" * 64
    fixture_now = datetime.now(UTC).replace(microsecond=0)
    artifact_created_at = (fixture_now - timedelta(minutes=1)).isoformat().replace(
        "+00:00", "Z"
    )
    artifact_expires_at = (fixture_now + timedelta(days=14)).isoformat().replace(
        "+00:00", "Z"
    )
    handoff = {
        "contractName": "chummer6-ui.preview-nightly-candidate-handoff",
        "contractVersion": 1,
        **producer_common,
        "artifactId": artifact_id,
        "artifactSha256": artifact_sha,
        "contentInventorySha256": sha256(content_inventory_path),
    }
    authenticated_api = {
        "contractName": "chummer6-ui.preview-nightly-candidate-authenticated-api",
        "contractVersion": 1,
        **producer_common,
        "artifactId": artifact_id,
        "artifactSha256": artifact_sha,
        "artifactCreatedAt": artifact_created_at,
        "artifactExpiresAt": artifact_expires_at,
        "event": "workflow_dispatch",
        "status": "completed",
        "conclusion": "success",
    }
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
            **producer_common,
            "artifactId": artifact_id,
            "artifactSha256": artifact_sha,
            "artifactCreatedAt": artifact_created_at,
            "artifactExpiresAt": artifact_expires_at,
            "manifestPath": MODULE.CANDIDATE_MANIFEST_PATH,
            "manifestSha256": sha256(stage / "RELEASE_CHANNEL.generated.json"),
            "contentInventorySha256": sha256(content_inventory_path),
            "exportReceiptSha256": sha256(export_receipt_path),
            "handoffSha256": MODULE._canonical_json_sha256(handoff),
            "authenticatedApiSha256": MODULE._canonical_json_sha256(authenticated_api),
            "contentInventory": {
                "path": MODULE.CANDIDATE_CONTENT_INVENTORY_PATH,
                "sha256": sha256(content_inventory_path),
                "sizeBytes": content_inventory_path.stat().st_size,
            },
            "exportReceipt": {
                "path": MODULE.CANDIDATE_EXPORT_PATH,
                "sha256": sha256(export_receipt_path),
                "sizeBytes": export_receipt_path.stat().st_size,
            },
            "supplyChain": copied_supply_chain,
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
    with zipfile.ZipFile(archive) as bundle:
        capture = json.loads(bundle.read(MODULE.NATIVE_CAPTURE_FILE_NAME))
    producer = capture["candidate"]
    fixture_now = datetime.now(UTC).replace(microsecond=0)
    default_created_at = (fixture_now - timedelta(minutes=1)).isoformat().replace(
        "+00:00", "Z"
    )
    default_expires_at = (fixture_now + timedelta(days=14)).isoformat().replace(
        "+00:00", "Z"
    )
    runs = {
        "900": {
            "workflow": MODULE.CANDIDATE_EXPORT_WORKFLOW,
            "actor": "producer-user",
            "artifact": "preview-nightly-candidate-900-1",
            "artifact_id": 503,
            "digest": "d" * 64,
            "created_at": producer["artifactCreatedAt"],
            "expires_at": producer["artifactExpiresAt"],
        },
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
                        "created_at": row.get("created_at", default_created_at),
                        "expires_at": row.get("expires_at", default_expires_at),
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
            "triggering_actor": {"login": row["actor"]},
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


def candidate_producer_validation_fixture(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> tuple[
    Path,
    Path,
    dict[tuple[str, str, str], dict],
    dict[str, dict],
    dict,
    dict,
]:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    authorities = MODULE.validate_authorities(presentation_root)
    stage = tmp_path / "candidate"
    tuples = write_current_stage(stage, native_windows=False)
    retained = write_retained_source(
        stage, [tuples[("avalonia", "windows", "win-x64")]]
    )
    write_inputs_and_candidate(stage, authorities, retained)
    evidence = tmp_path / "native-evidence"
    write_native_evidence_source(evidence, stage, tuples)
    archive = tmp_path / "native-evidence-finalized.zip"
    zip_evidence(evidence, archive)
    configure_github_api(monkeypatch, archive)
    _, inventory, _ = MODULE.validate_capture_inventory(evidence)
    capture = json.loads((evidence / MODULE.NATIVE_CAPTURE_FILE_NAME).read_text())
    inputs = json.loads((stage / MODULE.INPUT_FILE_NAME).read_text())
    authority = MODULE.verify_native_evidence_authority_receipt(inputs)
    return stage, evidence, tuples, inventory, capture["candidate"], authority


def test_v2_candidate_provenance_reconstructs_exact_compatibility_binding(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    native_fixture = load_test_fixture_module(
        "test_windows_native_evidence.py", "stage_contract_native_fixture"
    )

    stage, native_root, capture_args = native_fixture.make_fixture(tmp_path)
    native_fixture.upgrade_fixture_to_windows_only_scope(
        tmp_path, stage, capture_args
    )
    native_fixture.evidence.capture(capture_args)
    _, inventory, _ = MODULE.validate_capture_inventory(native_root)
    capture = json.loads(
        (native_root / MODULE.NATIVE_CAPTURE_FILE_NAME).read_text(encoding="utf-8")
    )
    manifest = json.loads(
        (stage / MODULE.CANDIDATE_MANIFEST_PATH).read_text(encoding="utf-8")
    )
    tuples = {
        (row["head"], row["platform"], row["rid"]): row
        for row in manifest["artifacts"]
    }
    authority = {
        "repository": "ArchonMegalon/chummer6-ui",
        "presentationCommit": native_fixture.CANDIDATE_SHA,
    }
    monkeypatch.setattr(
        MODULE,
        "_verify_candidate_producer_github_actions_provenance",
        lambda _candidate: {"status": "completed"},
    )
    monkeypatch.setattr(MODULE.SUPPLY_CHAIN, "verify_gate", lambda **_kwargs: None)
    monkeypatch.setattr(
        MODULE.SUPPLY_CHAIN,
        "content_bindings",
        lambda root: native_fixture.evidence.SUPPLY_CHAIN.content_bindings(root),
    )
    shutil.copytree(tmp_path / "incumbent", stage / "retained-source")
    MODULE.require_complete_windows_only_registry_shelf(stage)

    result = MODULE.validate_candidate_producer_provenance(
        stage,
        native_root,
        inventory,
        capture["candidate"],
        authority,
        tuples,
    )

    assert set(result["scopeBindings"]) == {
        "fullShelfCompatibilityManifest",
        "fullShelfManifest",
        "publicationScope",
        "signingReceipt",
    }
    compatibility = result["scopeBindings"]["fullShelfCompatibilityManifest"]
    assert compatibility["path"].endswith("/publication/releases.json")
    assert compatibility["sha256"] == capture["candidate"][
        "fullShelfCompatibilityManifestSha256"
    ]

    forged = json.loads(json.dumps(capture["candidate"]))
    forged["fullShelfCompatibilityManifest"]["sha256"] = "0" * 64
    with pytest.raises(MODULE.ContractError, match="exact copied bytes"):
        MODULE.validate_candidate_producer_provenance(
            stage,
            native_root,
            inventory,
            forged,
            authority,
            tuples,
        )


def test_v2_finalized_native_package_binds_independent_authenticode_receipt(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    native_fixture = load_test_fixture_module(
        "test_windows_native_evidence.py", "stage_contract_native_authenticode_fixture"
    )
    stage, native_root, capture_args = native_fixture.make_fixture(tmp_path)
    proposal = native_fixture.upgrade_fixture_to_windows_only_scope(
        tmp_path, stage, capture_args
    )
    native_fixture.evidence.capture(capture_args)
    finalized = tmp_path / "native-finalized"
    finalize = native_fixture.finalize_args(native_root, finalized)
    finalize.scope_approval_json = native_fixture.canonical_json(
        native_fixture.windows_only_approval(proposal, stage, native_root)
    )
    native_fixture.evidence.finalize(finalize)

    shutil.copytree(tmp_path / "incumbent", stage / "retained-source")
    MODULE.require_complete_windows_only_registry_shelf(stage)
    write_json(stage / MODULE.INPUT_FILE_NAME, {})
    manifest = json.loads(
        (stage / MODULE.CANDIDATE_MANIFEST_PATH).read_text(encoding="utf-8")
    )
    tuples = {
        (row["head"], row["platform"], row["rid"]): row
        for row in manifest["artifacts"]
    }
    authority = {
        "repository": "ArchonMegalon/chummer6-ui",
        "presentationCommit": native_fixture.CANDIDATE_SHA,
    }
    monkeypatch.setattr(
        MODULE, "verify_native_evidence_authority_receipt", lambda _inputs: authority
    )
    monkeypatch.setattr(
        MODULE,
        "_verify_candidate_producer_github_actions_provenance",
        lambda _candidate: {"status": "completed"},
    )
    def verified_provenance(_source: object, archive: Path | None = None) -> dict:
        payload = {"status": "completed"}
        if archive is not None:
            payload["artifactSha256"] = sha256(archive)
        return payload

    monkeypatch.setattr(MODULE, "verify_github_actions_provenance", verified_provenance)
    monkeypatch.setattr(MODULE.SUPPLY_CHAIN, "verify_gate", lambda **_kwargs: None)
    monkeypatch.setattr(
        MODULE.SUPPLY_CHAIN,
        "content_bindings",
        lambda root: native_fixture.evidence.SUPPLY_CHAIN.content_bindings(root),
    )
    archive = tmp_path / "authenticated-finalized.zip"
    zip_evidence(finalized, archive)

    package = MODULE._validate_finalized_native_evidence_extraction(
        stage, finalized, archive, manifest, tuples
    )

    binding = package["authenticodeVerification"]
    assert binding["path"] == MODULE.PUBLICATION_SCOPE.AUTHENTICODE_VERIFICATION_RELATIVE_PATH
    receipt = finalized / MODULE.NATIVE_AUTHENTICODE_RELATIVE_PATH
    assert binding["sha256"] == sha256(receipt)
    assert package["scopeApproval"]["payload"][
        "authenticodeVerificationSha256"
    ] == binding["sha256"]

    linux_row = next(row for row in manifest["artifacts"] if row["platform"] == "linux")
    (stage / "files" / linux_row["fileName"]).write_bytes(b"fresh-linux")
    (stage / "proof").mkdir()
    (stage / "startup-smoke").mkdir(exist_ok=True)
    write_json(
        stage / "startup-smoke/startup-smoke-avalonia-linux-x64.receipt.json",
        {
            "artifactDigest": f"sha256:{linux_row['sha256']}",
            "artifactFileName": linux_row["fileName"],
            "channelId": "preview",
            "headId": "avalonia",
            "platform": "linux",
            "rid": "linux-x64",
            "status": "passed",
            "version": proposal["release"]["version"],
        },
    )
    wrapper = MODULE.stage_native_evidence(stage, archive)
    root_finalization = stage / MODULE.NATIVE_FINALIZATION_FILE_NAME
    nested_finalization = (
        stage / "proof" / "windows-native" / MODULE.NATIVE_FINALIZATION_FILE_NAME
    )
    assert root_finalization.read_bytes() == nested_finalization.read_bytes()
    assert wrapper["nativeFinalization"] == {
        "path": MODULE.NATIVE_FINALIZATION_FILE_NAME,
        "sha256": sha256(root_finalization),
        "sizeBytes": root_finalization.stat().st_size,
    }
    visual_path = stage / "WINDOWS_INSTALLER_VISUAL_PROOF-avalonia-win-x64.generated.json"
    assert wrapper["visualProof"] == {
        "path": visual_path.name,
        "sha256": sha256(visual_path),
        "sizeBytes": visual_path.stat().st_size,
    }


def test_windows_only_upload_proof_custody_includes_raw_finalization_v2(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    stage = tmp_path / "candidate"
    stage.mkdir()
    write_json(
        stage / "NATIVE_WINDOWS_EVIDENCE.generated.json",
        {
            "nativeFinalization": {
                "path": MODULE.NATIVE_FINALIZATION_FILE_NAME,
                "sha256": "a" * 64,
                "sizeBytes": 1,
            }
        },
    )
    (stage / MODULE.NATIVE_FINALIZATION_FILE_NAME).write_bytes(b"raw-v2-finalization")
    for head in MODULE.PROMOTED_WINDOWS_HEADS:
        (
            stage
            / f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-win-x64.generated.json"
        ).write_bytes(f"visual-{head}".encode("utf-8"))
    semantic_proof = {
        "contractName": "fixture.upload-proof",
        "status": "passed",
    }
    monkeypatch.setattr(
        MODULE,
        "build_upload_semantic_proof",
        lambda _stage: semantic_proof,
    )

    MODULE.stage_upload_proof_receipts(stage)

    copied = (
        stage
        / "proof/nightly-stage"
        / MODULE.NATIVE_FINALIZATION_FILE_NAME
    )
    assert copied.read_bytes() == b"raw-v2-finalization"
    receipts = MODULE.verify_upload_proof_receipts(stage)
    assert receipts[MODULE.NATIVE_FINALIZATION_FILE_NAME] == sha256(copied)


def test_v2_run_candidate_replays_finalized_scope_and_excludes_fresh_linux(
    tmp_path: Path,
) -> None:
    scope_fixture = load_test_fixture_module(
        "test_preview_nightly_publication_scope.py", "stage_contract_scope_fixture"
    )

    values, proposal = scope_fixture.prepare(tmp_path)
    scope_fixture.finalize_for_test(tmp_path, values, proposal)
    stage = values["evidence_root"]

    candidate = MODULE.build_run_upload_candidate(stage)
    inventory_paths = {
        path.relative_to(stage / MODULE.PUBLICATION_SCOPE.PUBLICATION_DIRECTORY).as_posix()
        for path in MODULE.run_upload_inventory_paths(stage)
    }

    assert candidate["version"] == values["version"]
    assert candidate["uploadAuthorized"] is False
    assert candidate["deployAuthorized"] is False
    assert candidate["uploadRoot"] == MODULE.PUBLICATION_SCOPE.PUBLICATION_DIRECTORY
    assert "files/chummer-avalonia-win-x64-installer.exe" in inventory_paths
    assert "files/chummer-avalonia-win-x64-payload.zip" in inventory_paths
    assert "files/chummer-avalonia-osx-arm64-installer.dmg" in inventory_paths
    assert "files/chummer-avalonia-linux-x64-installer.deb" not in inventory_paths


def test_derive_stage_semantics_directly_replays_finalized_v2_scope(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    scope_fixture = load_test_fixture_module(
        "test_preview_nightly_publication_scope.py", "stage_contract_scope_fixture_replay"
    )

    values, proposal = scope_fixture.prepare(tmp_path)
    final_path = scope_fixture.finalize_for_test(tmp_path, values, proposal)
    stage = values["evidence_root"]
    shutil.copytree(stage / "build-files", stage / "files")
    shutil.copy2(values["paths"]["build_manifest"], stage / "RELEASE_CHANNEL.generated.json")
    shutil.copy2(values["paths"]["build_releases"], stage / "releases.json")
    shutil.copytree(values["incumbent_shelf"], stage / "retained-source")
    incumbent_platforms = proposal["incumbentSnapshot"]["platforms"]
    for name, rows_key in (
        ("RELEASE_CHANNEL.generated.json", "artifacts"),
        ("releases.json", "downloads"),
    ):
        path = stage / "retained-source" / name
        payload = json.loads(path.read_text(encoding="utf-8"))
        payload["desktopTupleCoverage"] = {
            "requiredDesktopHeads": ["avalonia"],
            "requiredDesktopPlatforms": incumbent_platforms,
        }
        assert {
            row.get("platformId", row["platform"]) for row in payload[rows_key]
        } == set(
            incumbent_platforms
        )
        write_json(path, payload)

    manifest = json.loads(
        (stage / "RELEASE_CHANNEL.generated.json").read_text(encoding="utf-8")
    )
    tuples = {
        (row["head"], row["platform"], row["rid"]): row
        for row in manifest["artifacts"]
    }
    pre_capture_binding = MODULE.verify_pre_capture_publication_scope(
        stage, manifest, tuples
    )
    inputs = {
        "authorities": [],
        "release": {
            "channel": "preview",
            "version": values["version"],
            "publishedAt": "2026-07-21T12:00:00Z",
        },
        "retainedShelf": {
            "canonicalSha256": sha256(
                stage / "retained-source" / "RELEASE_CHANNEL.generated.json"
            ),
            "compatibilitySha256": sha256(
                stage / "retained-source" / "releases.json"
            ),
        },
    }
    candidate_receipt = {
        "publicationScopeRequired": True,
        "publicationScope": pre_capture_binding,
    }
    for relative in (
        MODULE.AUTHORITATIVE_VALIDATION_FILE_NAME,
        "WINDOWS_BOOTSTRAP_NATIVE_SMOKE.generated.json",
        "WINDOWS_RELEASE_EVIDENCE.generated.json",
        "RELEASE_BUILD_HANDOFF.generated.json",
        "release-evidence/public-promotion.json",
    ):
        write_json(stage / relative, {"status": "passed"})
    write_json(
        stage / MODULE.RUN_UPLOAD_CANDIDATE_FILE_NAME,
        MODULE.build_run_upload_candidate(stage),
    )

    monkeypatch.setattr(
        MODULE, "verify_input_receipt", lambda _stage: (inputs, candidate_receipt)
    )
    monkeypatch.setattr(
        MODULE, "require_current_artifacts", lambda _stage: (manifest, tuples)
    )
    monkeypatch.setattr(
        MODULE, "verify_supply_chain_gate", lambda *_args: {"status": "passed"}
    )
    monkeypatch.setattr(
        MODULE,
        "verify_authoritative_validation_receipt",
        lambda *_args: {"status": "passed"},
    )
    for name in (
        "verify_compatibility_manifest",
        "verify_files_shelf_scope",
        "verify_retained_shelf_preservation",
        "verify_retained_files_inventory",
        "verify_current_startup_receipts",
    ):
        monkeypatch.setattr(MODULE, name, lambda *_args, **_kwargs: None)
    monkeypatch.setattr(
        MODULE,
        "verify_native_windows_evidence",
        lambda *_args: {
            "candidateProvenance": {
                "githubActionsProvenance": {"status": "completed"}
            },
            "treeSha256": "a" * 64,
        },
    )
    monkeypatch.setattr(
        MODULE, "verify_windows_exit_gates", lambda *_args: {"avalonia": "b" * 64}
    )
    monkeypatch.setattr(
        MODULE, "verify_windows_native_smoke_summary", lambda *_args: {"status": "pass"}
    )
    monkeypatch.setattr(
        MODULE, "verify_windows_release_summary", lambda *_args: {"status": "pass"}
    )
    monkeypatch.setattr(
        MODULE, "verify_release_build_handoff", lambda *_args: {"stage_proof_complete": True}
    )
    monkeypatch.setattr(
        MODULE, "verify_promotion_evidence", lambda *_args: {"status": "pass"}
    )
    monkeypatch.setattr(
        MODULE, "verify_upload_proof_receipts", lambda *_args: {"proof": "c" * 64}
    )

    semantics = MODULE.derive_stage_semantics(stage)

    assert semantics["publicationScope"]["registryFinalizeEligible"] is True
    assert semantics["publicationScope"]["publicationEligible"] is False
    assert semantics["checks"]["registryFinalizeEligible"] is True
    assert semantics["checks"]["registryFinalizeAuthorityUnavailable"] is False
    assert semantics["checks"]["uiPublicationEligibilityDenied"] is True
    assert semantics["checks"]["windowsOnlyPublicationDelta"] is True
    assert semantics["checks"]["freshLinuxExcludedFromPublication"] is True
    assert semantics["uploadBoundary"]["requiredUploadRoot"] == "publication"
    assert sha256(final_path) == semantics["proof"]["publicationScopeSha256"]


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
    retained_files = stage / "retained-source" / "files"
    retained_files.mkdir(parents=True, exist_ok=True)
    for name in sorted(retained_names):
        source = stage / "files" / name
        target = retained_files / name
        if source.resolve(strict=False) != target.resolve(strict=False):
            shutil.copy2(source, target)
    files_inventory = [
        {
            "path": name,
            "sha256": sha256(retained_files / name),
            "sizeBytes": (retained_files / name).stat().st_size,
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
    for head in MODULE.PROMOTED_WINDOWS_HEADS:
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
                for head in MODULE.PROMOTED_WINDOWS_HEADS
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
                for head in MODULE.PROMOTED_WINDOWS_HEADS
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
            "supplyChain": MODULE.SUPPLY_CHAIN.content_bindings(stage),
        },
    )


def make_valid_seal_stage(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> tuple[Path, Path, dict[tuple[str, str, str], dict]]:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    authorities = MODULE.validate_authorities(presentation_root)
    stage = tmp_path / "candidate"
    presentation_commit = next(
        row["commit"] for row in authorities if row["name"] == "presentation"
    )
    tuples = write_current_stage(
        stage,
        native_windows=False,
        source_commit=presentation_commit,
    )
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
                for head in MODULE.PROMOTED_WINDOWS_HEADS
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
    assert payload["candidateProvenance"]["githubActionsProvenance"]["artifactId"] == "503"
    assert len(payload["candidateProvenance"]["localCandidateFiles"]) == 8
    assert payload["githubActionsProvenance"]["candidateProducer"]["workflow"] == (
        MODULE.CANDIDATE_EXPORT_WORKFLOW
    )
    assert payload["githubActionsProvenance"]["finalization"]["artifactId"] == 502
    for head in MODULE.PROMOTED_WINDOWS_HEADS:
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


def test_seal_contract_archives_retained_noncurrent_tuple_outside_active_manifest(tmp_path: Path) -> None:
    stage = tmp_path / "candidate"
    incoming = write_current_stage(stage, native_windows=True)
    retained_artifact = stage / "retained-source" / "files" / "chummer-avalonia-osx-arm64-installer.dmg"
    retained_artifact.parent.mkdir(parents=True)
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

    MODULE.verify_retained_shelf_preservation(stage, incoming)
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())
    assert {row["platform"] for row in manifest["artifacts"]} == {"linux", "windows"}


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
    ("platform", "rid", "kind", "expected_error"),
    (
        ("windows", "win-x64", "msix", "unpromoted desktop head"),
        ("linux", "linux-x64", "installer", "unpromoted desktop head"),
        (
            "macos",
            "osx-arm64",
            "installer",
            "outside the active desktop platforms",
        ),
    ),
)
def test_unpromoted_retained_desktop_rows_are_rejected_before_preservation(
    tmp_path: Path, platform: str, rid: str, kind: str, expected_error: str
) -> None:
    stage = tmp_path / "candidate"
    write_current_stage(stage, native_windows=False)
    manifest_path = stage / "RELEASE_CHANNEL.generated.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    extra = dict(manifest["artifacts"][0])
    extra.update(
        {
            "artifactId": f"blazor-desktop-{rid}-{kind}",
            "head": "blazor-desktop",
            "headId": "blazor-desktop",
            "platform": platform,
            "rid": rid,
            "kind": kind,
            "fileName": f"chummer-blazor-desktop-{rid}.{kind}",
            "publicationState": "retained",
        }
    )
    manifest["artifacts"].append(extra)
    write_json(manifest_path, manifest)

    with pytest.raises(MODULE.ContractError, match=expected_error):
        MODULE.require_current_artifacts(stage)


def test_current_registry_target_is_accepted_but_macos_artifact_is_rejected(
    tmp_path: Path,
) -> None:
    stage = tmp_path / "candidate"
    write_current_stage(stage, native_windows=False)
    manifest_path = stage / "RELEASE_CHANNEL.generated.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    assert set(manifest["desktopTupleCoverage"]["requiredDesktopPlatforms"]) == {
        "linux",
        "windows",
    }
    MODULE.require_exact_promoted_desktop_scope(manifest)
    retained = dict(manifest["artifacts"][0])
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
    with pytest.raises(MODULE.ContractError, match="outside the active desktop platforms"):
        MODULE.require_exact_promoted_desktop_scope(manifest)


def test_current_registry_target_rejects_unknown_platform_artifact(
    tmp_path: Path,
) -> None:
    stage = tmp_path / "candidate"
    write_current_stage(stage, native_windows=False)
    manifest_path = stage / "RELEASE_CHANNEL.generated.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    unknown = dict(manifest["artifacts"][0])
    unknown.update(
        {
            "artifactId": "avalonia-freebsd-x64-installer",
            "platform": "freebsd",
            "rid": "freebsd-x64",
            "fileName": "chummer-avalonia-freebsd-x64-installer.tar.zst",
        }
    )
    manifest["artifacts"].append(unknown)
    with pytest.raises(MODULE.ContractError, match="outside the active desktop platforms"):
        MODULE.require_exact_promoted_desktop_scope(manifest)


@pytest.mark.parametrize("mutation", ("platform-alias-conflict", "wrong-linux-identity"))
def test_current_registry_target_rejects_inexact_active_desktop_artifact_identity(
    tmp_path: Path, mutation: str
) -> None:
    stage = tmp_path / "candidate"
    write_current_stage(stage, native_windows=False)
    manifest_path = stage / "RELEASE_CHANNEL.generated.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if mutation == "platform-alias-conflict":
        manifest["artifacts"][0]["platformId"] = "macos"
    else:
        linux_row = next(
            row for row in manifest["artifacts"] if row["platform"] == "linux"
        )
        linux_row["artifactId"] = "avalonia-linux-x64-package"
        linux_row["fileName"] = "forged-linux-installer.deb"
    with pytest.raises(MODULE.ContractError, match="platform identity|artifact identity"):
        MODULE.require_exact_promoted_desktop_scope(manifest)


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


@pytest.mark.parametrize(
    "mutation",
    (
        "wrong_contract",
        "wrong_contract_version",
        "duplicate_roles",
        "wrong_release",
        "wrong_digest",
        "wrong_platform",
        "wrong_head",
        "wrong_rid",
        "wrong_checks",
        "wrong_reviewer",
    ),
)
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
    elif mutation == "wrong_contract_version":
        proof["contractVersion"] = 2
    elif mutation == "duplicate_roles":
        proof["screenshots"][1]["role"] = "progress"
    elif mutation == "wrong_release":
        proof["version"] = "run-forged"
    elif mutation == "wrong_digest":
        proof["artifactDigest"] = "sha256:" + "f" * 64
    elif mutation == "wrong_platform":
        proof["platform"] = "linux"
    elif mutation == "wrong_head":
        proof["head"] = proof["headId"] = "forged-head"
    elif mutation == "wrong_rid":
        proof["rid"] = "win-arm64"
    elif mutation == "wrong_checks":
        proof["checks"]["capture_mode"] = "automated"
    else:
        proof["review"]["authenticatedReviewer"] = "forged-reviewer"
    write_json(path, proof)
    finalization_path = evidence / MODULE.NATIVE_FINALIZATION_FILE_NAME
    finalization = json.loads(finalization_path.read_text(encoding="utf-8"))
    proof_binding = next(
        row for row in finalization["proofs"] if row["headId"] == "avalonia"
    )
    proof_binding["sha256"] = sha256(path)
    write_json(finalization_path, finalization)
    finalized_inventory_path = evidence / MODULE.NATIVE_FINALIZED_INVENTORY_FILE_NAME
    finalized_inventory = json.loads(
        finalized_inventory_path.read_text(encoding="utf-8")
    )
    finalized_inventory["files"] = MODULE.inventory_tree(
        evidence,
        exclusions=(MODULE.NATIVE_FINALIZED_INVENTORY_FILE_NAME,),
    )
    write_json(finalized_inventory_path, finalized_inventory)
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


@pytest.mark.parametrize(
    "abuse",
    (
        "duplicate",
        "traversal",
        "symlink",
        "special",
        "type_mismatch",
        "compression_ratio",
        "too_many_members",
    ),
)
def test_finalized_evidence_safe_extraction_rejects_zip_abuse(
    tmp_path: Path,
    abuse: str,
) -> None:
    archive = tmp_path / f"unsafe-{abuse}.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_STORED) as bundle:
        if abuse == "duplicate":
            with warnings.catch_warnings():
                warnings.simplefilter("ignore", UserWarning)
                bundle.writestr("duplicate.txt", b"first")
                bundle.writestr("duplicate.txt", b"second")
        elif abuse == "traversal":
            bundle.writestr("../escape.txt", b"escape")
        elif abuse == "symlink":
            info = zipfile.ZipInfo("link")
            info.create_system = 3
            info.external_attr = (stat.S_IFLNK | 0o777) << 16
            bundle.writestr(info, b"target")
        elif abuse == "special":
            info = zipfile.ZipInfo("named-pipe")
            info.create_system = 3
            info.external_attr = (stat.S_IFIFO | 0o600) << 16
            bundle.writestr(info, b"")
        elif abuse == "type_mismatch":
            info = zipfile.ZipInfo("declared-directory")
            info.create_system = 3
            info.external_attr = (stat.S_IFDIR | 0o700) << 16
            bundle.writestr(info, b"")
        elif abuse == "compression_ratio":
            bundle.writestr(
                "compressed-bomb.bin",
                b"0" * (2 * 1024 * 1024),
                compress_type=zipfile.ZIP_DEFLATED,
            )
        else:
            for index in range(MODULE.EVIDENCE_ARCHIVE_MAX_FILES + 1):
                bundle.writestr(f"member-{index:03d}.txt", b"x")

    with pytest.raises(MODULE.ContractError, match="finalized evidence archive"):
        MODULE.extract_evidence_archive(archive, tmp_path / "extracted")
    assert not (tmp_path / "escape.txt").exists()


def test_finalized_evidence_snapshot_rejects_symlink_source(tmp_path: Path) -> None:
    if not hasattr(os, "O_NOFOLLOW"):
        pytest.skip("platform has no O_NOFOLLOW")
    real_archive = tmp_path / "real-finalized.zip"
    real_archive.write_bytes(b"not-needed-for-no-follow-check")
    linked_archive = tmp_path / "linked-finalized.zip"
    linked_archive.symlink_to(real_archive)
    replay_root = tmp_path / "replay"
    replay_root.mkdir(mode=0o700)

    with pytest.raises(
        MODULE.ContractError,
        match="could not create finalized evidence archive snapshot",
    ):
        MODULE._snapshot_finalized_evidence_archive(linked_archive, replay_root)


def test_finalized_evidence_snapshot_rejects_special_source_without_blocking(
    tmp_path: Path,
) -> None:
    if not hasattr(os, "mkfifo") or not hasattr(os, "O_NONBLOCK"):
        pytest.skip("platform cannot create a nonblocking FIFO fixture")
    special_archive = tmp_path / "finalized-evidence.fifo"
    os.mkfifo(special_archive, mode=0o600)
    replay_root = tmp_path / "replay"
    replay_root.mkdir(mode=0o700)

    with pytest.raises(
        MODULE.ContractError,
        match="finalized evidence archive descriptor is not a regular file",
    ):
        MODULE._snapshot_finalized_evidence_archive(special_archive, replay_root)


def test_standalone_verify_rejects_recomputed_whitespace_only_staged_tree_mutation(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    _, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    native_root = stage / "proof" / "windows-native"
    finalized_inventory = native_root / MODULE.NATIVE_FINALIZED_INVENTORY_FILE_NAME
    finalized_inventory.write_bytes(finalized_inventory.read_bytes() + b" \n")

    receipt_path = stage / "NATIVE_WINDOWS_EVIDENCE.generated.json"
    receipt = json.loads(receipt_path.read_text())
    rows = MODULE.inventory_tree(native_root)
    receipt["treeSha256"] = MODULE.inventory_sha256(rows)
    receipt["fileCount"] = len(rows)
    receipt["finalizedInventorySha256"] = sha256(finalized_inventory)
    write_json(receipt_path, receipt)
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())

    with pytest.raises(
        MODULE.ContractError,
        match="staged native Windows evidence differs from the original finalized archive",
    ):
        MODULE.verify_native_windows_evidence(stage, manifest, tuples)


def test_standalone_verify_rejects_archive_mutation_during_replay(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    _, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())
    original_validate = MODULE._validate_finalized_native_evidence_extraction

    def validate_then_mutate_archive(*args, **kwargs):
        result = original_validate(*args, **kwargs)
        archive = args[2]
        archive.chmod(0o600)
        archive.write_bytes(archive.read_bytes() + b"post-validation-mutation")
        return result

    monkeypatch.setattr(
        MODULE,
        "_validate_finalized_native_evidence_extraction",
        validate_then_mutate_archive,
    )

    with pytest.raises(
        MODULE.ContractError,
        match="archive snapshot (?:identity or metadata )?changed",
    ):
        MODULE.verify_native_windows_evidence(stage, manifest, tuples)


def test_standalone_verify_rejects_caller_archive_path_swap_and_restore(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    _, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    native_root = stage / "proof" / "windows-native"
    finalized_inventory = native_root / MODULE.NATIVE_FINALIZED_INVENTORY_FILE_NAME
    finalized_inventory.write_bytes(finalized_inventory.read_bytes() + b" \n")

    receipt_path = stage / "NATIVE_WINDOWS_EVIDENCE.generated.json"
    receipt = json.loads(receipt_path.read_text())
    rows = MODULE.inventory_tree(native_root)
    receipt["treeSha256"] = MODULE.inventory_sha256(rows)
    receipt["fileCount"] = len(rows)
    receipt["finalizedInventorySha256"] = sha256(finalized_inventory)
    write_json(receipt_path, receipt)

    archive = stage / "proof" / "windows-native-finalized.zip"
    authenticated_sha256 = sha256(archive)
    replacement_archive = tmp_path / "replacement-finalized.zip"
    parked_archive = tmp_path / "parked-authenticated-finalized.zip"
    zip_evidence(native_root, replacement_archive)
    replacement_sha256 = sha256(replacement_archive)
    assert replacement_sha256 != authenticated_sha256

    original_copy = MODULE._copy_archive_descriptor_to_snapshot
    swaps: list[bool] = []

    def copy_while_caller_path_is_swapped(
        source_fd: int,
        snapshot_fd: int,
        expected_size: int,
    ) -> str:
        archive.replace(parked_archive)
        replacement_archive.replace(archive)
        try:
            swaps.append(True)
            return original_copy(source_fd, snapshot_fd, expected_size)
        finally:
            archive.replace(replacement_archive)
            parked_archive.replace(archive)

    monkeypatch.setattr(
        MODULE,
        "_copy_archive_descriptor_to_snapshot",
        copy_while_caller_path_is_swapped,
    )
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())

    with pytest.raises(
        MODULE.ContractError,
        match=(
            "finalized evidence archive descriptor changed while snapshotting|"
            "staged native Windows evidence differs from the original finalized archive"
        ),
    ):
        MODULE.verify_native_windows_evidence(stage, manifest, tuples)

    assert swaps == [True]
    assert sha256(archive) == authenticated_sha256
    assert sha256(replacement_archive) == replacement_sha256


def test_standalone_verify_rejects_persistent_source_mutation_during_snapshot(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    _, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    archive = stage / "proof" / "windows-native-finalized.zip"
    original_copy = MODULE._copy_archive_descriptor_to_snapshot
    mutations: list[bool] = []

    def mutate_source_then_copy(
        source_fd: int,
        snapshot_fd: int,
        expected_size: int,
    ) -> str:
        with archive.open("ab") as handle:
            handle.write(b"persistent-source-mutation")
        mutations.append(True)
        return original_copy(source_fd, snapshot_fd, expected_size)

    monkeypatch.setattr(
        MODULE,
        "_copy_archive_descriptor_to_snapshot",
        mutate_source_then_copy,
    )
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())

    with pytest.raises(
        MODULE.ContractError,
        match="finalized evidence archive changed size while snapshotting",
    ):
        MODULE.verify_native_windows_evidence(stage, manifest, tuples)

    assert mutations == [True]


def test_standalone_verify_hashes_only_private_archive_snapshot(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    _, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    archive = stage / "proof" / "windows-native-finalized.zip"
    original_sha256_file = MODULE.sha256_file
    snapshot_hashes: list[Path] = []

    def reject_caller_archive_hash(path: Path) -> str:
        candidate = Path(path)
        if candidate == archive:
            raise AssertionError("caller archive path was reopened for hashing")
        if candidate.name == "finalized-evidence.snapshot.zip":
            snapshot_hashes.append(candidate)
        return original_sha256_file(candidate)

    monkeypatch.setattr(MODULE, "sha256_file", reject_caller_archive_hash)
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())

    assert MODULE.verify_native_windows_evidence(stage, manifest, tuples)["status"] == "passed"
    assert snapshot_hashes


def test_standalone_verify_identity_safely_cleans_private_archive_replay(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    _, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())
    original_consume = MODULE.consume_owned_directory
    replay_roots: list[Path] = []

    def record_identity_safe_cleanup(
        source: Path,
        quarantine: Path,
        *,
        expected_device: int,
        expected_inode: int,
    ) -> None:
        if source.name.startswith("preview-nightly-evidence-replay-"):
            assert MODULE.directory_identity(source) == {
                "device": expected_device,
                "inode": expected_inode,
            }
            replay_roots.append(source)
        original_consume(
            source,
            quarantine,
            expected_device=expected_device,
            expected_inode=expected_inode,
        )

    monkeypatch.setattr(MODULE, "consume_owned_directory", record_identity_safe_cleanup)
    MODULE.verify_native_windows_evidence(stage, manifest, tuples)

    assert len(replay_roots) == 1
    assert all(not path.exists() for path in replay_roots)


@pytest.mark.parametrize(
    ("mutation", "value"),
    (
        ("workflow", ".github/workflows/forged.yml"),
        ("ref", "main"),
        ("sha", "A" * 40),
        ("runAttempt", "01"),
        ("artifactId", "0"),
        ("artifactName", "preview-nightly-candidate-900"),
        ("artifactSha256", "sha256:" + "d" * 64),
        ("handoffSha256", "e" * 64),
        ("authenticatedApiSha256", "e" * 64),
        ("artifactExpiresAt", "2026-07-18T09:59:59Z"),
    ),
)
def test_candidate_producer_capture_binding_rejects_adversarial_identity(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    mutation: str,
    value: object,
) -> None:
    stage, evidence, tuples, inventory, candidate, authority = (
        candidate_producer_validation_fixture(tmp_path, monkeypatch)
    )
    candidate[mutation] = value

    with pytest.raises(MODULE.ContractError):
        MODULE.validate_candidate_producer_provenance(
            stage, evidence, inventory, candidate, authority, tuples
        )


@pytest.mark.parametrize("mutation", ("wrong_contract", "missing_row", "wrong_digest", "extra_row"))
def test_candidate_content_inventory_rejects_nonexact_five_file_contract(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    mutation: str,
) -> None:
    stage, evidence, tuples, inventory, candidate, authority = (
        candidate_producer_validation_fixture(tmp_path, monkeypatch)
    )
    path = evidence / MODULE.CANDIDATE_CONTENT_INVENTORY_PATH
    payload = json.loads(path.read_text())
    if mutation == "wrong_contract":
        payload["contractName"] = "forged.contract"
    elif mutation == "missing_row":
        payload["files"].pop()
    elif mutation == "wrong_digest":
        payload["files"][0]["sha256"] = "f" * 64
    else:
        payload["files"].append(
            {"path": "files/extra.exe", "sha256": "f" * 64, "sizeBytes": 1}
        )
    write_json(path, payload)
    digest = sha256(path)
    inventory[MODULE.CANDIDATE_CONTENT_INVENTORY_PATH] = {
        "path": MODULE.CANDIDATE_CONTENT_INVENTORY_PATH,
        "sha256": digest,
        "sizeBytes": path.stat().st_size,
    }
    candidate["contentInventorySha256"] = digest
    candidate["contentInventory"] = {
        "path": MODULE.CANDIDATE_CONTENT_INVENTORY_PATH,
        "sha256": digest,
        "sizeBytes": path.stat().st_size,
    }

    with pytest.raises(MODULE.ContractError, match="content inventory contract"):
        MODULE.validate_candidate_producer_provenance(
            stage, evidence, inventory, candidate, authority, tuples
        )


@pytest.mark.parametrize(
    "mutation",
    (
        "wrong_contract",
        "wrong_source",
        "wrong_inventory",
        "wrong_head",
        "structural_supply_chain",
        "integer_release_authority",
    ),
)
def test_candidate_export_receipt_rejects_adversarial_contract_binding(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    mutation: str,
) -> None:
    stage, evidence, tuples, inventory, candidate, authority = (
        candidate_producer_validation_fixture(tmp_path, monkeypatch)
    )
    path = evidence / MODULE.CANDIDATE_EXPORT_PATH
    payload = json.loads(path.read_text())
    if mutation == "wrong_contract":
        payload["contractName"] = "forged.contract"
    elif mutation == "wrong_source":
        payload["source"]["actor"] = "forged-user"
    elif mutation == "wrong_inventory":
        payload["contentInventory"]["sha256"] = "f" * 64
    elif mutation == "structural_supply_chain":
        payload["supplyChainVerification"] = {
            "mode": MODULE.SUPPLY_CHAIN.STRUCTURAL_VERIFICATION_MODE,
            "releaseAuthoritative": False,
        }
    elif mutation == "integer_release_authority":
        payload["supplyChainVerification"]["releaseAuthoritative"] = 1
    else:
        payload["heads"][0]["installer"]["sizeBytes"] += 1
    write_json(path, payload)
    digest = sha256(path)
    inventory[MODULE.CANDIDATE_EXPORT_PATH] = {
        "path": MODULE.CANDIDATE_EXPORT_PATH,
        "sha256": digest,
        "sizeBytes": path.stat().st_size,
    }
    candidate["exportReceiptSha256"] = digest
    candidate["exportReceipt"] = {
        "path": MODULE.CANDIDATE_EXPORT_PATH,
        "sha256": digest,
        "sizeBytes": path.stat().st_size,
    }

    with pytest.raises(MODULE.ContractError, match="candidate export receipt"):
        MODULE.validate_candidate_producer_provenance(
            stage, evidence, inventory, candidate, authority, tuples
        )


@pytest.mark.parametrize(
    "mutation",
    (
        "workflow",
        "branch",
        "sha",
        "attempt",
        "actor",
        "repository",
        "event",
        "status",
        "conclusion",
        "artifact_id",
        "artifact_name",
        "artifact_digest",
        "artifact_expired",
        "artifact_created",
        "artifact_expires",
        "pagination",
        "count_underflow",
        "pagination_over_100",
    ),
)
def test_candidate_producer_github_api_replay_fails_closed(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    mutation: str,
) -> None:
    _, _, _, _, candidate, _ = candidate_producer_validation_fixture(
        tmp_path, monkeypatch
    )
    base_fetch = MODULE.fetch_github_api_json

    def fetch(url: str) -> dict:
        payload = json.loads(json.dumps(base_fetch(url)))
        if url.endswith("/artifacts?per_page=100"):
            artifact = payload["artifacts"][0]
            if mutation == "artifact_id":
                artifact["id"] = 999
            elif mutation == "artifact_name":
                artifact["name"] = "forged-artifact"
            elif mutation == "artifact_digest":
                artifact["digest"] = "sha256:" + "f" * 64
            elif mutation == "artifact_expired":
                artifact["expired"] = True
            elif mutation == "artifact_created":
                artifact["created_at"] = "2026-07-18T10:00:01Z"
            elif mutation == "artifact_expires":
                artifact["expires_at"] = "2099-07-25T10:00:01Z"
            elif mutation == "pagination":
                payload["total_count"] = 2
            elif mutation == "count_underflow":
                payload["total_count"] = 0
            elif mutation == "pagination_over_100":
                payload["artifacts"] = payload["artifacts"] * 101
                payload["total_count"] = 101
            return payload
        if mutation == "workflow":
            payload["path"] = ".github/workflows/forged.yml"
        elif mutation == "branch":
            payload["head_branch"] = "other"
        elif mutation == "sha":
            payload["head_sha"] = "f" * 40
        elif mutation == "attempt":
            payload["run_attempt"] = 2
        elif mutation == "actor":
            payload["actor"]["login"] = "forged-user"
        elif mutation == "repository":
            payload["repository"]["full_name"] = "fixture/forged"
        elif mutation == "event":
            payload["event"] = "push"
        elif mutation == "status":
            payload["status"] = "in_progress"
        elif mutation == "conclusion":
            payload["conclusion"] = "failure"
        return payload

    monkeypatch.setattr(MODULE, "fetch_github_api_json", fetch)

    with pytest.raises(MODULE.ContractError, match="candidate producer GitHub Actions"):
        MODULE._verify_candidate_producer_github_actions_provenance(candidate)


def test_candidate_producer_rejects_created_at_beyond_five_minute_skew(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    stage, evidence, tuples, inventory, candidate, authority = (
        candidate_producer_validation_fixture(tmp_path, monkeypatch)
    )
    fixture_now = datetime.now(UTC).replace(microsecond=0)
    candidate["artifactCreatedAt"] = (
        fixture_now + timedelta(minutes=6)
    ).isoformat().replace("+00:00", "Z")
    candidate["artifactExpiresAt"] = (
        fixture_now + timedelta(days=14)
    ).isoformat().replace("+00:00", "Z")

    with pytest.raises(MODULE.ContractError, match="more than five minutes in the future"):
        MODULE.validate_candidate_producer_provenance(
            stage, evidence, inventory, candidate, authority, tuples
        )


def test_candidate_producer_validation_rejects_staged_byte_snapshot_mutation(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    stage, evidence, tuples, inventory, candidate, authority = (
        candidate_producer_validation_fixture(tmp_path, monkeypatch)
    )
    original_verify = MODULE._verify_candidate_producer_github_actions_provenance
    installer = stage / "files" / "chummer-avalonia-win-x64-installer.exe"

    def verify_then_mutate(raw_candidate: dict) -> dict:
        result = original_verify(raw_candidate)
        installer.write_bytes(installer.read_bytes() + b"post-API-snapshot-mutation")
        return result

    monkeypatch.setattr(
        MODULE,
        "_verify_candidate_producer_github_actions_provenance",
        verify_then_mutate,
    )

    with pytest.raises(MODULE.ContractError, match="changed during validation"):
        MODULE.validate_candidate_producer_provenance(
            stage, evidence, inventory, candidate, authority, tuples
        )


@pytest.mark.parametrize(
    ("branch", "source_ref", "opposite_ref"),
    (
        ("main", "refs/heads/main", "refs/tags/main"),
        ("v1.2.3", "refs/tags/v1.2.3", "refs/heads/v1.2.3"),
    ),
)
def test_github_workflow_run_path_matcher_accepts_only_exact_ref_kind(
    branch: str, source_ref: str, opposite_ref: str
) -> None:
    bare = MODULE.NATIVE_CAPTURE_WORKFLOW
    sha = "a" * 40
    accepted = (
        bare,
        f"{bare}@{branch}",
        f"{bare}@{source_ref}",
        f"{bare}@{sha}",
    )
    rejected = (
        f"{bare}@{opposite_ref}",
        f"{bare}@refs/heads/other",
        f"{bare}@{branch}-other",
        f"{bare}@{branch}/other",
        f"{bare}@{sha}0",
        f"{bare}@{sha.upper()}",
        f"{bare}-other@{branch}",
        f"prefix/{bare}@{branch}",
        f"{bare}@@{branch}",
        f" {bare}",
        f"{bare} ",
    )

    assert all(
        MODULE.github_workflow_run_path_matches(
            actual,
            bare,
            branch=branch,
            ref=source_ref,
            sha=sha,
        )
        for actual in accepted
    )
    assert not any(
        MODULE.github_workflow_run_path_matches(
            actual,
            bare,
            branch=branch,
            ref=source_ref,
            sha=sha,
        )
        for actual in rejected
    )
    assert not MODULE.github_workflow_run_path_matches(
        bare,
        bare,
        branch=branch,
        ref=f" {source_ref}",
        sha=sha,
    )
    assert not MODULE.github_workflow_run_path_matches(
        bare,
        bare,
        branch=branch,
        ref=source_ref,
        sha=f"{sha} ",
    )
    assert not MODULE.github_workflow_run_path_matches(
        bare,
        bare,
        branch=branch,
        ref=source_ref,
        sha=sha.upper(),
    )


@pytest.mark.parametrize(
    "source_ref",
    (
        "main",
        "refs/heads/",
        "refs/tags/",
        "refs/pull/1/head",
        "refs/heads/main/",
        "refs/heads/main..other",
        "refs/heads/main//other",
        "refs/heads/main.lock",
        "refs/heads/topic.lock/subtopic",
        "refs/heads/.hidden",
        "refs/heads/topic/.hidden",
        "refs/tags/release.lock/candidate",
        " refs/heads/main",
        "refs/heads/main ",
    ),
)
def test_github_workflow_source_rejects_bare_or_malformed_ref(source_ref: str) -> None:
    source = {
        "repository": "fixture/chummer6-ui",
        "workflow": MODULE.NATIVE_CAPTURE_WORKFLOW,
        "runId": "1001",
        "runAttempt": "1",
        "ref": source_ref,
        "sha": "a" * 40,
        "actor": "capture-user",
        "triggeringActor": "capture-user",
        "rerunPolicy": "same-actor-only",
        "artifactName": "windows-native-evidence-1001-1",
    }
    authority = {
        "repository": source["repository"],
        "presentationCommit": source["sha"],
    }

    with pytest.raises(MODULE.ContractError, match="exact full refs/heads"):
        MODULE.validate_github_workflow_source(
            source,
            label="capture",
            authority=authority,
            workflow=MODULE.NATIVE_CAPTURE_WORKFLOW,
            artifact_prefix="windows-native-evidence",
        )


@pytest.mark.parametrize(
    "source_sha",
    (
        "a" * 39,
        "a" * 41,
        "A" * 40,
        " " + "a" * 40,
        "a" * 40 + " ",
    ),
)
def test_github_workflow_source_rejects_nonexact_sha(source_sha: str) -> None:
    source = {
        "repository": "fixture/chummer6-ui",
        "workflow": MODULE.NATIVE_CAPTURE_WORKFLOW,
        "runId": "1001",
        "runAttempt": "1",
        "ref": "refs/heads/main",
        "sha": source_sha,
        "actor": "capture-user",
        "triggeringActor": "capture-user",
        "rerunPolicy": "same-actor-only",
        "artifactName": "windows-native-evidence-1001-1",
    }
    authority = {
        "repository": source["repository"],
        "presentationCommit": "a" * 40,
    }

    with pytest.raises(MODULE.ContractError, match="exact lowercase 40-character"):
        MODULE.validate_github_workflow_source(
            source,
            label="capture",
            authority=authority,
            workflow=MODULE.NATIVE_CAPTURE_WORKFLOW,
            artifact_prefix="windows-native-evidence",
        )


def test_github_workflow_source_accepts_exact_actions_bot_actor() -> None:
    source = {
        "repository": "fixture/chummer6-ui",
        "workflow": MODULE.NATIVE_CAPTURE_WORKFLOW,
        "runId": "1001",
        "runAttempt": "1",
        "ref": "refs/heads/main",
        "sha": "a" * 40,
        "actor": "github-actions[bot]",
        "triggeringActor": "github-actions[bot]",
        "rerunPolicy": "same-actor-only",
        "artifactName": "windows-native-evidence-1001-1",
    }

    assert MODULE.validate_github_workflow_source(
        source,
        label="capture",
        authority={
            "repository": source["repository"],
            "presentationCommit": source["sha"],
        },
        workflow=MODULE.NATIVE_CAPTURE_WORKFLOW,
        artifact_prefix="windows-native-evidence",
    )["actor"] == "github-actions[bot]"


@pytest.mark.parametrize(
    ("field", "value"),
    (
        ("triggeringActor", "different-operator"),
        ("rerunPolicy", "any-actor"),
    ),
)
def test_github_workflow_source_rejects_unbound_rerun_provenance(
    field: str, value: str
) -> None:
    source = {
        "repository": "fixture/chummer6-ui",
        "workflow": MODULE.NATIVE_CAPTURE_WORKFLOW,
        "runId": "1001",
        "runAttempt": "1",
        "ref": "refs/heads/main",
        "sha": "a" * 40,
        "actor": "capture-user",
        "triggeringActor": "capture-user",
        "rerunPolicy": "same-actor-only",
        "artifactName": "windows-native-evidence-1001-1",
    }
    source[field] = value

    with pytest.raises(MODULE.ContractError, match="same-actor"):
        MODULE.validate_github_workflow_source(
            source,
            label="capture",
            authority={
                "repository": source["repository"],
                "presentationCommit": source["sha"],
            },
            workflow=MODULE.NATIVE_CAPTURE_WORKFLOW,
            artifact_prefix="windows-native-evidence",
        )


@pytest.mark.parametrize(
    "actor",
    (
        "GitHub-actions[bot]",
        "github-actions[Bot]",
        "github-actions[bot] ",
        "[github-actions[bot]]",
        "github-actions[-bot]",
        "github-actions-",
    ),
)
def test_github_workflow_source_rejects_actions_bot_lookalikes(actor: str) -> None:
    source = {
        "repository": "fixture/chummer6-ui",
        "workflow": MODULE.NATIVE_CAPTURE_WORKFLOW,
        "runId": "1001",
        "runAttempt": "1",
        "ref": "refs/heads/main",
        "sha": "a" * 40,
        "actor": actor,
        "triggeringActor": actor,
        "rerunPolicy": "same-actor-only",
        "artifactName": "windows-native-evidence-1001-1",
    }

    with pytest.raises(MODULE.ContractError, match="actor is not a GitHub login"):
        MODULE.validate_github_workflow_source(
            source,
            label="capture",
            authority={
                "repository": source["repository"],
                "presentationCommit": source["sha"],
            },
            workflow=MODULE.NATIVE_CAPTURE_WORKFLOW,
            artifact_prefix="windows-native-evidence",
        )


@pytest.mark.parametrize(
    ("workflow", "run_id", "actor", "artifact_name", "branch", "source_ref", "path_suffix"),
    (
        (
            MODULE.NATIVE_CAPTURE_WORKFLOW,
            "1001",
            "capture-user",
            "windows-native-evidence-1001-1",
            "main",
            "refs/heads/main",
            "main",
        ),
        (
            MODULE.NATIVE_FINALIZATION_WORKFLOW,
            "2002",
            "fixture-reviewer",
            "windows-native-evidence-finalized-2002-1",
            "v1.2.3",
            "refs/tags/v1.2.3",
            "refs/tags/v1.2.3",
        ),
        (
            MODULE.NATIVE_FINALIZATION_WORKFLOW,
            "2002",
            "fixture-reviewer",
            "windows-native-evidence-finalized-2002-1",
            "v1.2.3",
            "refs/tags/v1.2.3",
            None,
        ),
    ),
)
def test_capture_and_finalization_provenance_accept_exact_bound_run_paths(
    monkeypatch: pytest.MonkeyPatch,
    workflow: str,
    run_id: str,
    actor: str,
    artifact_name: str,
    branch: str,
    source_ref: str,
    path_suffix: str | None,
) -> None:
    fixture_now = datetime.now(UTC).replace(microsecond=0)
    source = {
        "repository": "fixture/chummer6-ui",
        "workflow": workflow,
        "runId": run_id,
        "runAttempt": "1",
        "ref": source_ref,
        "sha": "a" * 40,
        "actor": actor,
        "triggeringActor": actor,
        "rerunPolicy": "same-actor-only",
        "artifactName": artifact_name,
    }
    run = {
        "id": int(run_id),
        "path": workflow if path_suffix is None else f"{workflow}@{path_suffix}",
        "head_sha": source["sha"],
        "run_attempt": 1,
        "event": "workflow_dispatch",
        "status": "completed",
        "conclusion": "success",
        "head_branch": branch,
        "actor": {"login": actor},
        "triggering_actor": {"login": actor},
        "repository": {"full_name": source["repository"]},
    }
    artifact = {
        "id": int(run_id) + 100,
        "name": artifact_name,
        "expired": False,
        "digest": "sha256:" + "b" * 64,
        "created_at": (fixture_now - timedelta(minutes=1)).isoformat().replace(
            "+00:00", "Z"
        ),
        "expires_at": (fixture_now + timedelta(days=14)).isoformat().replace(
            "+00:00", "Z"
        ),
        "workflow_run": {"id": int(run_id), "head_sha": source["sha"]},
    }
    monkeypatch.setattr(
        MODULE,
        "fetch_github_api_json",
        lambda url: {"total_count": 1, "artifacts": [artifact]}
        if url.endswith("/artifacts?per_page=100")
        else run,
    )

    provenance = MODULE.verify_github_actions_provenance(source)

    assert provenance["workflow"] == workflow
    assert provenance["ref"] == source_ref


@pytest.mark.parametrize(
    "mutation",
    (
        "workflow",
        "opposite_ref_kind",
        "path_suffix_junk",
        "path_sha_case",
        "bare_source_ref",
        "source_sha_padding",
        "head_sha_case",
        "head_sha_padding",
        "artifact_head_sha_padding",
        "actor",
        "triggering_actor",
        "event",
        "event_padding",
        "expired",
        "artifact_id",
        "artifact_count_underflow",
        "artifact_count_overflow",
        "artifact_pagination_over_100",
        "artifact_created_future",
        "artifact_expiry_out_of_order",
    ),
)
def test_github_actions_api_provenance_fails_closed(
    monkeypatch: pytest.MonkeyPatch, mutation: str
) -> None:
    fixture_now = datetime.now(UTC).replace(microsecond=0)
    source = {
        "repository": "fixture/chummer6-ui",
        "workflow": MODULE.NATIVE_CAPTURE_WORKFLOW,
        "runId": "1001",
        "runAttempt": "1",
        "ref": "refs/heads/main",
        "sha": "a" * 40,
        "actor": "capture-user",
        "triggeringActor": "capture-user",
        "rerunPolicy": "same-actor-only",
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
        "triggering_actor": {"login": source["triggeringActor"]},
        "repository": {"full_name": source["repository"]},
    }
    artifact = {
        "id": 501,
        "name": source["artifactName"],
        "expired": False,
        "digest": "sha256:" + "b" * 64,
        "created_at": (fixture_now - timedelta(minutes=1)).isoformat().replace(
            "+00:00", "Z"
        ),
        "expires_at": (fixture_now + timedelta(days=14)).isoformat().replace(
            "+00:00", "Z"
        ),
        "workflow_run": {"id": 1001, "head_sha": source["sha"]},
    }
    artifact_total_count = 1
    artifact_rows = [artifact]
    if mutation == "workflow":
        run["path"] = ".github/workflows/forged.yml"
    elif mutation == "opposite_ref_kind":
        run["path"] = f"{source['workflow']}@refs/tags/main"
    elif mutation == "path_suffix_junk":
        run["path"] = f"{source['workflow']}@main/other"
    elif mutation == "path_sha_case":
        run["path"] = f"{source['workflow']}@{source['sha'].upper()}"
    elif mutation == "bare_source_ref":
        source["ref"] = "main"
    elif mutation == "source_sha_padding":
        source["sha"] += " "
    elif mutation == "head_sha_case":
        run["head_sha"] = source["sha"].upper()
    elif mutation == "head_sha_padding":
        run["head_sha"] += " "
    elif mutation == "artifact_head_sha_padding":
        artifact["workflow_run"]["head_sha"] += " "
    elif mutation == "actor":
        run["actor"] = {"login": "forged-user"}
    elif mutation == "triggering_actor":
        run["triggering_actor"] = {"login": "forged-user"}
    elif mutation == "event":
        run["event"] = "push"
    elif mutation == "event_padding":
        run["event"] = "workflow_dispatch "
    elif mutation == "expired":
        artifact["expired"] = True
    elif mutation == "artifact_count_underflow":
        artifact_total_count = 0
    elif mutation == "artifact_count_overflow":
        artifact_total_count = 2
    elif mutation == "artifact_pagination_over_100":
        artifact_rows = artifact_rows * 101
        artifact_total_count = 101
    elif mutation == "artifact_created_future":
        artifact["created_at"] = (
            fixture_now + timedelta(minutes=6)
        ).isoformat().replace("+00:00", "Z")
    elif mutation == "artifact_expiry_out_of_order":
        artifact["expires_at"] = (
            fixture_now - timedelta(minutes=2)
        ).isoformat().replace("+00:00", "Z")
    else:
        artifact["id"] = 0
    monkeypatch.setattr(
        MODULE,
        "fetch_github_api_json",
        lambda url: {"total_count": artifact_total_count, "artifacts": artifact_rows}
        if url.endswith("/artifacts?per_page=100")
        else run,
    )

    with pytest.raises(MODULE.ContractError, match="GitHub Actions"):
        MODULE.verify_github_actions_provenance(source)


def test_retained_noncurrent_tuple_cannot_be_reintroduced_into_active_manifest(tmp_path: Path) -> None:
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

    with pytest.raises(MODULE.ContractError, match="reintroduced retained non-current tuple"):
        MODULE.verify_retained_shelf_preservation(stage, incoming)


@pytest.mark.parametrize("mutation", ("remove", "mutate", "receipt_digest", "receipt_count"))
def test_retained_inventory_binds_auxiliary_files_and_summary(
    tmp_path: Path, mutation: str
) -> None:
    stage = tmp_path / "candidate"
    write_current_stage(stage, native_windows=True)
    retained_file = stage / "retained-source" / "files" / "retained-shelf-metadata.json"
    retained_file.parent.mkdir(parents=True)
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
    (stage / "UI_WINDOWS_DESKTOP_EXIT_GATE-avalonia-win-x64.generated.json").unlink()

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
        "9ab907a19a0536979bf6dbce3d5f8e22f40ec264d91da7b71f810323b6cacf73"
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


def test_standalone_verify_replays_candidate_producer_and_seal_binds_provenance(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, stage, _ = make_valid_seal_stage(tmp_path, monkeypatch)
    seal = MODULE.seal_stage(presentation_root, stage)
    native = json.loads((stage / "NATIVE_WINDOWS_EVIDENCE.generated.json").read_text())

    assert seal["proof"]["candidateProducerProvenance"] == native[
        "candidateProvenance"
    ]
    assert seal["checks"]["candidateProducerAuthenticated"] is True
    assert seal["proof"]["candidateProducerProvenance"]["localCandidateFiles"] == (
        MODULE._candidate_local_content_rows(stage)
    )

    base_fetch = MODULE.fetch_github_api_json

    def producer_run_fails_after_seal(url: str) -> dict:
        payload = json.loads(json.dumps(base_fetch(url)))
        if url.endswith("/actions/runs/900"):
            payload["conclusion"] = "failure"
        return payload

    monkeypatch.setattr(
        MODULE, "fetch_github_api_json", producer_run_fails_after_seal
    )

    with pytest.raises(
        MODULE.ContractError,
        match="candidate producer GitHub Actions workflow-run provenance differs",
    ):
        MODULE.verify_seal(stage)


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


def test_authoritative_validator_snapshot_materializes_all_exact_commit_bytes_privately(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    authorities = MODULE.validate_authorities(presentation_root)
    bindings = MODULE.revalidate_authoritative_validator_sources(
        presentation_root, authorities
    )
    snapshot_root = tmp_path / "snapshot"
    snapshot_root.mkdir(mode=0o755)

    paths = MODULE.materialize_authoritative_validator_snapshot(
        snapshot_root,
        authorities,
        bindings,
    )

    assert set(paths) == {row[0] for row in MODULE.AUTHORITATIVE_VALIDATOR_FILES}
    commits = {row["name"]: row["commit"] for row in authorities}
    roots = {
        name: Path(os.environ[root_env])
        for name, root_env, _ in MODULE.AUTHORITY_ENVIRONMENTS
    }
    assert stat.S_IMODE(snapshot_root.stat().st_mode) == 0o700
    for source_name, authority_name, relative in MODULE.AUTHORITATIVE_VALIDATOR_FILES:
        path = paths[source_name]
        assert path == snapshot_root / authority_name / relative
        assert path.read_bytes() == MODULE.committed_file_bytes(
            roots[authority_name], commits[authority_name], relative
        )
        assert sha256(path) == bindings[source_name]["sha256"]
        assert stat.S_IMODE(path.stat().st_mode) == 0o600
        for parent in path.parents:
            if parent == snapshot_root.parent:
                break
            assert stat.S_IMODE(parent.stat().st_mode) == 0o700


def test_authoritative_replay_uses_snapshot_during_worktree_change_and_restore(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    authorities = MODULE.validate_authorities(presentation_root)
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())
    mutable_validator = (
        presentation_root / "scripts" / "materialize-windows-desktop-exit-gate.sh"
    )
    original_bytes = mutable_validator.read_bytes()
    real_run = MODULE.subprocess.run
    real_registry_loader = MODULE._load_registry_materializer
    invoked_snapshot: Path | None = None
    registry_snapshot: Path | None = None
    presentation_invocations: list[Path] = []

    def record_registry_snapshot(path: Path):
        nonlocal registry_snapshot
        registry_snapshot = path
        assert "preview-nightly-authority-snapshot-" in str(path)
        return real_registry_loader(path)

    def change_restore_around_snapshot(command, *args, **kwargs):
        nonlocal invoked_snapshot
        if isinstance(command, (list, tuple)) and len(command) >= 2:
            command_path = Path(command[1])
            if command_path.name in {
                "materialize-windows-desktop-exit-gate.sh",
                "verify-windows-release-evidence.py",
                "materialize_release_candidate_handoff.py",
            }:
                presentation_invocations.append(command_path)
                assert "preview-nightly-authority-snapshot-" in str(command_path)
                if command_path.name == "materialize_release_candidate_handoff.py":
                    assert (
                        command_path.with_name(
                            "materialize_windows_visual_proof_handoff.py"
                        )
                    ).is_file()
        if (
            invoked_snapshot is None
            and isinstance(command, (list, tuple))
            and len(command) >= 2
            and command[0] == "bash"
            and Path(command[1]).name == mutable_validator.name
        ):
            invoked_snapshot = Path(command[1])
            assert invoked_snapshot != mutable_validator
            mutable_validator.write_text("#!/usr/bin/env bash\nexit 97\n", encoding="utf-8")
            try:
                return real_run(command, *args, **kwargs)
            finally:
                mutable_validator.write_bytes(original_bytes)
        return real_run(command, *args, **kwargs)

    monkeypatch.setattr(MODULE, "_load_registry_materializer", record_registry_snapshot)
    monkeypatch.setattr(MODULE.subprocess, "run", change_restore_around_snapshot)

    result = MODULE.replay_authoritative_stage_validators(
        presentation_root,
        stage,
        manifest,
        tuples,
        authorities,
    )

    assert result["status"] == "passed"
    assert invoked_snapshot is not None
    assert "preview-nightly-authority-snapshot-" in str(invoked_snapshot)
    assert not invoked_snapshot.exists()
    assert mutable_validator.read_bytes() == original_bytes
    assert registry_snapshot is not None
    assert not registry_snapshot.exists()
    assert [path.name for path in presentation_invocations] == [
        "materialize-windows-desktop-exit-gate.sh",
        "verify-windows-release-evidence.py",
        "materialize_release_candidate_handoff.py",
    ]
    assert all(not path.exists() for path in presentation_invocations)


def test_authoritative_replay_normalizes_snapshot_paths_and_cleans_snapshot(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root, stage, tuples = make_valid_seal_stage(tmp_path, monkeypatch)
    authorities = MODULE.validate_authorities(presentation_root)
    manifest = json.loads((stage / "RELEASE_CHANNEL.generated.json").read_text())
    real_temporary_directory = MODULE.tempfile.TemporaryDirectory
    snapshot_roots: list[Path] = []

    def recording_temporary_directory(*args, **kwargs):
        temporary_directory = real_temporary_directory(*args, **kwargs)
        if kwargs.get("prefix") == "preview-nightly-authority-snapshot-":
            snapshot_roots.append(Path(temporary_directory.name))
        return temporary_directory

    monkeypatch.setattr(
        MODULE.tempfile,
        "TemporaryDirectory",
        recording_temporary_directory,
    )

    MODULE.replay_authoritative_stage_validators(
        presentation_root,
        stage,
        manifest,
        tuples,
        authorities,
    )

    assert len(snapshot_roots) == 1
    snapshot_text = str(snapshot_roots[0])
    assert not snapshot_roots[0].exists()
    generated_paths = (
        stage / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json",
        stage / "UI_WINDOWS_DESKTOP_EXIT_GATE-avalonia-win-x64.generated.json",
        stage / "WINDOWS_RELEASE_EVIDENCE.generated.json",
        stage / "RELEASE_BUILD_HANDOFF.generated.json",
        stage / "RELEASE_BUILD_HANDOFF.generated.md",
        stage / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json",
        stage / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md",
    )
    for path in generated_paths:
        assert path.is_file()
        assert snapshot_text not in path.read_text(encoding="utf-8")

    visual_handoff = json.loads(
        (stage / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json").read_text()
    )
    assert visual_handoff["repo_root"] == str(presentation_root)
    assert visual_handoff["capture_script_path"] == str(
        presentation_root / "scripts" / "capture-windows-installer-visual-proof.ps1"
    )
    assert snapshot_text not in json.dumps(visual_handoff, sort_keys=True)


def test_authoritative_validator_snapshot_is_cleaned_after_ordinary_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    presentation_root = configure_authorities(monkeypatch, tmp_path / "sources")
    authorities = MODULE.validate_authorities(presentation_root)
    stage = tmp_path / "candidate"
    stage.mkdir()
    real_temporary_directory = MODULE.tempfile.TemporaryDirectory
    snapshot_roots: list[Path] = []

    def recording_temporary_directory(*args, **kwargs):
        temporary_directory = real_temporary_directory(*args, **kwargs)
        if kwargs.get("prefix") == "preview-nightly-authority-snapshot-":
            snapshot_roots.append(Path(temporary_directory.name))
        return temporary_directory

    def ordinary_failure(*args, **kwargs):
        raise ValueError("fixture replay failure")

    monkeypatch.setattr(
        MODULE.tempfile,
        "TemporaryDirectory",
        recording_temporary_directory,
    )
    monkeypatch.setattr(
        MODULE,
        "_replay_authoritative_stage_validators_from_snapshot",
        ordinary_failure,
    )

    with pytest.raises(ValueError, match="fixture replay failure"):
        MODULE.replay_authoritative_stage_validators(
            presentation_root,
            stage,
            {},
            {},
            authorities,
        )

    assert len(snapshot_roots) == 1
    assert not snapshot_roots[0].exists()


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
    assert "CHUMMER_PUBLISHED_FEED_SOURCES" in source
    assert "export CHUMMER_VERIFY_MODE=slice" in source
    assert "export CHUMMER_USE_LOCAL_COMPATIBILITY_TREE=1" in source
    assert "export CHUMMER_ALLOW_STUB_PACKAGES=0" in source
    assert 'NUGET_PACKAGES="$CANDIDATE_DIR/work/nuget-packages"' in source
    assert "invalidate_reference_assembly_caches" in source
    assert "acquire_package_plane_lock" in source
    assert "CHUMMER_PACKAGE_PLANE_LOCK_HELD=1" in source
    assert "prepare_stage() (" in source
    assert "CHUMMER_PREVIEW_NIGHTLY_NATIVE_WINDOWS_EVIDENCE_ARCHIVE" in source
    assert source.count("unset GH_TOKEN GITHUB_TOKEN") == 2
    assert "candidate-producer, native-capture, and finalization provenance" in source
    assert "authenticated upstream visual reviewer" in source
    assert 'CANDIDATE_DIR="$sealing_work"' in source
    assert "candidate changed while creating transactional seal copy" in source
    assert "install-verified-sealed-dir-no-replace" in source
    assert "--expected-tree-sha256" in source
    assert '--expected-device "$sealing_work_device"' in source
    assert '--expected-inode "$sealing_work_inode"' in source
    assert 'rm -rf -- "$CANDIDATE_DIR"' not in source
    assert 'mv -- "$CANDIDATE_DIR" "$STAGE_DIR"' not in source
    assert source.count('--windows-exit-gate "$CANDIDATE_DIR/UI_WINDOWS_DESKTOP_EXIT_GATE-') == 1
    assert "--allow-proof-only-visual-handoff" not in source
    assert source.count("curl ") == 1
    assert '"$OSV_SCANNER_URL"' in source
    assert "https://github.com/google/osv-scanner/releases/download/" in source
    assert MODULE.SUPPLY_CHAIN.OSV_SCANNER_VERSION in source
    assert MODULE.SUPPLY_CHAIN.OSV_SCANNER_SHA256 in source
    assert "cp --no-preserve=mode,ownership,timestamps" in source
    assert "upload-sessions" not in source
    assert "publish-latest-nightly-to-downloads" not in source
    assert "RELEASE_UPLOAD_TICKET" not in source
    for invocation in (
        "publish_project avalonia \"$REPO_ROOT/Chummer.Avalonia/Chummer.Avalonia.csproj\" win-x64",
        "publish_project avalonia \"$REPO_ROOT/Chummer.Avalonia/Chummer.Avalonia.csproj\" linux-x64",
    ):
        assert invocation in source


def test_stage_package_plane_configuration_executes_as_explicit_candidate_only_local_tree(
    tmp_path: Path,
) -> None:
    source = ORCHESTRATOR_PATH.read_text(encoding="utf-8")
    start = source.index("configure_exact_package_plane() {")
    end = source.index("\n}\n\nacquire_package_plane_lock", start) + 3
    function = source[start:end]
    roots = {
        "CHUMMER_CORE_ROOT": tmp_path / "core",
        "CHUMMER_RUN_ROOT": tmp_path / "run",
        "CHUMMER_HUB_REGISTRY_ROOT": tmp_path / "registry",
        "CHUMMER_UI_KIT_ROOT": tmp_path / "ui-kit",
        "CHUMMER_MEDIA_FACTORY_ROOT": tmp_path / "media",
    }
    candidate = tmp_path / "candidate"
    repo = tmp_path / "presentation"
    package_version = "0.0.0-packageplane.20260720.1"
    version_helper = (
        roots["CHUMMER_CORE_ROOT"]
        / "scripts"
        / "ai"
        / "bootstrap-owner-contracts-feed.py"
    )
    version_helper.parent.mkdir(parents=True)
    version_helper.write_text(
        "#!/usr/bin/env python3\n"
        f"print({package_version!r})\n",
        encoding="utf-8",
    )
    environment = os.environ.copy()
    environment.update({key: str(value) for key, value in roots.items()})
    environment.update(
        {
            "CANDIDATE_DIR": str(candidate),
            "REPO_ROOT": str(repo),
            "CHUMMER_PUBLISHED_FEED_SOURCES": "https://packages.invalid/v3/index.json",
        }
    )
    completed = subprocess.run(
        ["bash", "-c", f"set -euo pipefail\n{function}\nconfigure_exact_package_plane\nenv -0"],
        env=environment,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    assert completed.returncode == 0, completed.stderr.decode(errors="replace")
    configured = dict(
        row.split("=", 1)
        for row in completed.stdout.decode().split("\0")
        if "=" in row
    )
    assert configured["CHUMMER_VERIFY_MODE"] == "slice"
    assert configured["CHUMMER_USE_LOCAL_COMPATIBILITY_TREE"] == "1"
    assert configured["CHUMMER_ALLOW_STUB_PACKAGES"] == "0"
    assert configured["CHUMMER_ENGINE_CONTRACTS_PACKAGE_VERSION"] == package_version
    assert configured["CHUMMER_CONTRACTS_PACKAGE_VERSION"] == package_version
    assert configured["CHUMMER_RUN_CONTRACTS_PACKAGE_VERSION"] == package_version
    assert configured["CHUMMER_HUB_REGISTRY_CONTRACTS_PACKAGE_VERSION"] == package_version
    assert "CHUMMER_PUBLISHED_FEED_SOURCES" not in configured
    assert configured["CHUMMER_LOCAL_CONTRACTS_PROJECT"] == str(
        roots["CHUMMER_CORE_ROOT"] / "Chummer.Contracts/Chummer.Contracts.csproj"
    )


def test_manifest_generator_has_fail_closed_stage_only_secondary_sync_boundary() -> None:
    source = MANIFEST_GENERATOR_PATH.read_text(encoding="utf-8")
    assert 'MANIFEST_STAGE_ONLY="${CHUMMER_RELEASE_MANIFEST_STAGE_ONLY:-0}"' in source
    assert 'if ! to_bool "$MANIFEST_STAGE_ONLY"; then\n  sync_portal_outputs' in source
    assert (
        "stage-only manifest generation skipped portal, run-services, presentation, and registry publication sync"
        in source
    )
