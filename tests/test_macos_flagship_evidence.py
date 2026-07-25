from __future__ import annotations

import base64
import hashlib
import importlib.util
import json
import os
import re
import subprocess
import sys
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
TOOL = REPO_ROOT / "scripts" / "macos_flagship_evidence.py"
RUNNER = REPO_ROOT / "scripts" / "run-macos-flagship-evidence.sh"
ESCROW_TOOL = REPO_ROOT / "scripts" / "macos_flagship_candidate_escrow.mjs"
INSTALLER_BUILDER = REPO_ROOT / "scripts" / "build-desktop-installer.sh"
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "macos-flagship-evidence.yml"
RUNBOOK = REPO_ROOT / "docs" / "MAC_CODEX_RELEASE_TO_CHUMMER_RUN.md"
NOW = "2026-07-25T12:00:00Z"


def load_tool_module():
    spec = importlib.util.spec_from_file_location(
        "macos_flagship_evidence_contract", TOOL
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def digest_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def digest_file(path: Path) -> str:
    return digest_bytes(path.read_bytes())


def canonical(payload: dict) -> bytes:
    return json.dumps(
        payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False
    ).encode("utf-8")


def write_canonical(path: Path, payload: dict) -> Path:
    path.write_bytes(canonical(payload))
    return path


def write_json(path: Path, payload: dict) -> Path:
    path.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    return path


def run_tool(*args: object) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        (sys.executable, str(TOOL), *(str(arg) for arg in args)),
        cwd=REPO_ROOT,
        check=False,
        capture_output=True,
        text=True,
    )


def authority(scope_raw: bytes) -> dict:
    source_sha = "1" * 40
    predecessor_sha = digest_bytes(canonical(predecessor()))
    return {
        "authorizedAtUtc": "2026-07-25T11:55:00Z",
        "candidateId": "candidate-20260725",
        "contractName": "chummer6-ui.macos-flagship-build-authority",
        "contractVersion": 1,
        "coreCommit": "2" * 40,
        "coreRef": "main",
        "expiresAtUtc": "2026-07-26T11:55:00Z",
        "head": "avalonia",
        "generationId": "generation-20260725",
        "hubCommit": "3" * 40,
        "hubRef": "main",
        "launchTarget": "Chummer.Avalonia",
        "legacyCommit": "7" * 40,
        "legacyRef": "Docker",
        "mediaFactoryCommit": "6" * 40,
        "mediaFactoryRef": "main",
        "predecessorSelectionAuthority": (
            "governance://global-flagship/n-minus-one/"
            "run-20260724-120000/to/run-20260725-120000/sha256/"
            + predecessor_sha
        ),
        "ref": "refs/heads/main",
        "registryCommit": "5" * 40,
        "registryRef": "main",
        "releaseChannel": "preview",
        "releaseVersion": "run-20260725-120000",
        "repository": "ArchonMegalon/chummer6-ui",
        "rid": "osx-arm64",
        "runnerNonce": "flagship20260725",
        "scopeDecisionAuthority": (
            "design://release-scope/flagship/sha256/" + digest_bytes(scope_raw)
        ),
        "scopeDecisionSha256": digest_bytes(scope_raw),
        "sha": source_sha,
        "uiCommit": source_sha,
        "uiKitCommit": "4" * 40,
        "uiKitRef": "main",
        "uiRef": "main",
        "workflow": ".github/workflows/macos-flagship-evidence.yml",
    }


def predecessor(
    artifact: bytes = b"predecessor-dmg",
    manifest_raw: bytes | None = None,
) -> dict:
    if manifest_raw is None:
        manifest_raw = b"placeholder"
    return {
        "artifactFileName": "chummer-avalonia-osx-arm64-installer.dmg",
        "artifactId": "avalonia-osx-arm64-installer",
        "artifactSha256": digest_bytes(artifact),
        "artifactSizeBytes": len(artifact),
        "artifactUrl": (
            "https://chummer.run/downloads/g/g-predecessor/files/"
            "chummer-avalonia-osx-arm64-installer.dmg"
        ),
        "contractName": "chummer6-ui.macos-predecessor-handoff",
        "contractVersion": 1,
        "generationId": "g-predecessor",
        "head": "avalonia",
        "releaseManifestSha256": digest_bytes(manifest_raw),
        "releaseManifestUrl": (
            "https://chummer.run/downloads/g/g-predecessor/"
            "RELEASE_CHANNEL.generated.json"
        ),
        "releaseVersion": "run-20260724-120000",
        "rid": "osx-arm64",
    }


def validation_fixture(tmp_path: Path) -> tuple[Path, Path, Path, Path, Path]:
    tmp_path.mkdir(parents=True, exist_ok=True)
    scope = tmp_path / "scope.json"
    scope.write_bytes(
        canonical(
            {
                "approvedAtUtc": "2026-07-25T11:50:00Z",
                "approvedBy": "independent-release-reviewer",
                "channel": "preview",
                "contractName": "chummer.release-scope-decision/v1",
                "contractVersion": 1,
                "decisionId": "macos-flagship-20260725",
                "platforms": [
                    {
                        "artifactAccessClass": "open_public",
                        "fallbackHeads": ["blazor-desktop"],
                        "platform": "macos",
                        "primaryHead": "avalonia",
                        "rid": "osx-arm64",
                        "signingRequirement": "signed",
                    }
                ],
                "releaseTarget": "preview",
                "releaseVersion": "run-20260725-120000",
                "status": "approved",
                "supportOwner": "chummer-release-operations",
            }
        )
        + b"\n"
    )
    auth = write_canonical(tmp_path / "authority.json", authority(scope.read_bytes()))
    prior = write_canonical(tmp_path / "predecessor.json", predecessor())
    output = tmp_path / "validation.json"
    github_env = tmp_path / "github.env"
    github_env.touch()
    return scope, auth, prior, output, github_env


def validate_command(
    scope: Path, auth: Path, prior: Path, output: Path, github_env: Path
) -> tuple[object, ...]:
    return (
        "validate-authority",
        "--authority",
        auth,
        "--scope-decision",
        scope,
        "--predecessor",
        prior,
        "--expected-repository",
        "ArchonMegalon/chummer6-ui",
        "--expected-ref",
        "refs/heads/main",
        "--expected-sha",
        "1" * 40,
        "--expected-actor",
        "release-operator",
        "--now",
        NOW,
        "--github-env",
        github_env,
        "--output",
        output,
    )


def test_validate_authority_accepts_fresh_canonical_pins(tmp_path: Path) -> None:
    fixture = validation_fixture(tmp_path)

    result = run_tool(*validate_command(*fixture))

    assert result.returncode == 0, result.stderr
    receipt = json.loads(fixture[3].read_text(encoding="utf-8"))
    assert receipt["status"] == "pass"
    assert receipt["nonPublishing"] == {
        "countsAsPublicationEvidence": False,
        "evidenceArtifactUploadAllowed": True,
        "publicActivationAttempted": False,
        "publicationAttempted": False,
        "releaseUploadAttempted": False,
    }
    environment = fixture[4].read_text(encoding="utf-8")
    assert "CHUMMER_MAC_RELEASE_STAGE_ONLY=1" in environment
    assert "CHUMMER_UI_EXPECTED_COMMIT=" + "1" * 40 in environment
    assert "CHUMMER_HUB_EXPECTED_COMMIT=" + "3" * 40 in environment
    assert "CHUMMER_SERVICES_" not in environment
    assert receipt["bootstrapSource"] == {
        "commit": "3" * 40,
        "ref": "main",
        "repository": "ArchonMegalon/chummer6-hub",
        "script": "scripts/run-mac-release-bootstrap.sh",
    }
    assert "CHUMMER_RELEASE_UPLOAD_TOKEN" not in environment


def test_validate_authority_rejects_noncanonical_or_expired_authority(
    tmp_path: Path,
) -> None:
    scope, auth, prior, output, github_env = validation_fixture(tmp_path)
    payload = json.loads(auth.read_text(encoding="utf-8"))
    auth.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    result = run_tool(
        *validate_command(scope, auth, prior, output, github_env)
    )
    assert result.returncode != 0
    assert "canonical JSON" in result.stderr

    write_canonical(auth, payload | {"expiresAtUtc": "2026-07-25T11:59:59Z"})
    result = run_tool(
        *validate_command(scope, auth, prior, output, github_env)
    )
    assert result.returncode != 0
    assert "expired" in result.stderr


def test_validate_authority_rejects_scope_or_predecessor_drift(
    tmp_path: Path,
) -> None:
    scope, auth, prior, output, github_env = validation_fixture(tmp_path)
    scope.write_bytes(scope.read_bytes() + b" ")
    result = run_tool(
        *validate_command(scope, auth, prior, output, github_env)
    )
    assert result.returncode != 0
    assert "canonical compact JSON plus LF" in result.stderr

    scope, auth, prior, output, github_env = validation_fixture(
        tmp_path / "second"
    )
    payload = json.loads(prior.read_text(encoding="utf-8"))
    payload["artifactUrl"] = "https://attacker.invalid/candidate.dmg"
    write_canonical(prior, payload)
    result = run_tool(
        *validate_command(scope, auth, prior, output, github_env)
    )
    assert result.returncode != 0
    assert "chummer.run URL" in result.stderr


def test_validate_authority_rejects_unapproved_or_self_approved_scope(
    tmp_path: Path,
) -> None:
    scope, auth, prior, output, github_env = validation_fixture(tmp_path)
    payload = json.loads(scope.read_text(encoding="utf-8"))
    payload["status"] = "draft"
    scope.write_bytes(canonical(payload) + b"\n")
    write_canonical(auth, authority(scope.read_bytes()))
    result = run_tool(
        *validate_command(scope, auth, prior, output, github_env)
    )
    assert result.returncode != 0
    assert "does not approve" in result.stderr

    scope, auth, prior, output, github_env = validation_fixture(
        tmp_path / "second"
    )
    payload = json.loads(scope.read_text(encoding="utf-8"))
    payload["approvedBy"] = "release-operator"
    scope.write_bytes(canonical(payload) + b"\n")
    write_canonical(auth, authority(scope.read_bytes()))
    result = run_tool(
        *validate_command(scope, auth, prior, output, github_env)
    )
    assert result.returncode != 0
    assert "independent approver" in result.stderr


def test_validate_authority_rejects_non_main_or_fork_context(
    tmp_path: Path,
) -> None:
    scope, auth, prior, output, github_env = validation_fixture(tmp_path)
    command = list(validate_command(scope, auth, prior, output, github_env))
    command[command.index("refs/heads/main")] = "refs/heads/release-candidate"
    result = run_tool(*command)
    assert result.returncode != 0
    assert "restricted to chummer6-ui main" in result.stderr

    command = list(validate_command(scope, auth, prior, output, github_env))
    command[command.index("ArchonMegalon/chummer6-ui")] = (
        "untrusted/chummer6-ui"
    )
    result = run_tool(*command)
    assert result.returncode != 0
    assert "restricted to chummer6-ui main" in result.stderr


def test_verify_predecessor_binds_manifest_and_artifact(tmp_path: Path) -> None:
    artifact_bytes = b"exact notarized predecessor"
    artifact = tmp_path / "chummer-avalonia-osx-arm64-installer.dmg"
    artifact.write_bytes(artifact_bytes)
    row = {
        "arch": "arm64",
        "artifactId": "avalonia-osx-arm64-installer",
        "fileName": artifact.name,
        "head": "avalonia",
        "platform": "macos",
        "rid": "osx-arm64",
        "sha256": digest_bytes(artifact_bytes),
        "sizeBytes": len(artifact_bytes),
    }
    manifest_payload = {
        "artifacts": [row],
        "generationId": "g-predecessor",
        "version": "run-20260724-120000",
    }
    manifest = write_json(tmp_path / "manifest.json", manifest_payload)
    handoff = predecessor(artifact_bytes, manifest.read_bytes())
    predecessor_path = write_canonical(
        tmp_path / "predecessor.json", handoff
    )
    output = tmp_path / "verification.json"

    result = run_tool(
        "verify-predecessor",
        "--predecessor",
        predecessor_path,
        "--manifest",
        manifest,
        "--artifact",
        artifact,
        "--output",
        output,
    )

    assert result.returncode == 0, result.stderr
    receipt = json.loads(output.read_text(encoding="utf-8"))
    assert receipt["status"] == "pass"
    assert receipt["artifact"]["sha256"] == digest_bytes(artifact_bytes)

    artifact.write_bytes(artifact_bytes + b"tamper")
    result = run_tool(
        "verify-predecessor",
        "--predecessor",
        predecessor_path,
        "--manifest",
        manifest,
        "--artifact",
        artifact,
        "--output",
        output,
    )
    assert result.returncode != 0
    assert "exact handoff" in result.stderr


def test_emit_signing_identity_binds_certificate_and_notary(
    tmp_path: Path,
) -> None:
    candidate = tmp_path / "chummer-avalonia-osx-arm64-installer.dmg"
    candidate.write_bytes(b"signed candidate")
    github = {
        "actor": "release-operator",
        "ref": "refs/heads/main",
        "repository": "ArchonMegalon/chummer6-ui",
        "sha": "1" * 40,
        "workflow": ".github/workflows/macos-flagship-evidence.yml",
    }
    authority_receipt = write_json(
        tmp_path / "authority.json",
        {
            "candidateId": "candidate-20260725",
            "contractName": "chummer6-ui.macos-flagship-authority-validation",
            "contractVersion": 1,
            "generationId": "generation-20260725",
            "github": github,
            "releaseVersion": "run-20260725-120000",
            "rid": "osx-arm64",
            "status": "pass",
        },
    )
    signing = write_json(
        tmp_path / "signing.json",
        {
            "artifacts": [
                {
                    "fileName": candidate.name,
                    "notarizationStatus": "pass",
                    "sha256": digest_file(candidate),
                    "signingStatus": "pass",
                }
            ],
            "contractName": "chummer6-ui.desktop_artifact_signing",
            "contractVersion": 2,
            "releaseVersion": "run-20260725-120000",
            "rid": "osx-arm64",
        },
    )
    notary = write_json(
        tmp_path / "notary.json",
        {
            "id": "01234567-89ab-cdef-0123-456789abcdef",
            "status": "Accepted",
        },
    )
    output = tmp_path / "identity.json"
    command = (
        "emit-signing-identity",
        "--authority-receipt",
        authority_receipt,
        "--candidate-artifact",
        candidate,
        "--signing-receipt",
        signing,
        "--notary-result",
        notary,
        "--identity",
        "Developer ID Application: Example (ABCDE12345)",
        "--team-id",
        "ABCDE12345",
        "--certificate-sha256",
        "a" * 64,
        "--certificate-spki-sha256",
        "b" * 64,
        "--expected-certificate-sha256",
        "a" * 64,
        "--expected-certificate-spki-sha256",
        "b" * 64,
        "--output",
        output,
    )
    result = run_tool(*command)
    assert result.returncode == 0, result.stderr
    receipt = json.loads(output.read_text(encoding="utf-8"))
    assert receipt["certificate"]["sha256"] == "a" * 64
    assert (
        receipt["notarization"]["submissionId"]
        == "01234567-89ab-cdef-0123-456789abcdef"
    )

    rejected = list(command)
    rejected[rejected.index("b" * 64)] = "c" * 64
    result = run_tool(*rejected)
    assert result.returncode != 0
    assert "fingerprint or SPKI pin mismatch" in result.stderr


def collect_fixture(tmp_path: Path) -> dict[str, Path]:
    tmp_path.mkdir(parents=True, exist_ok=True)
    source = tmp_path / "chummer-avalonia-osx-arm64-installer.dmg"
    source.write_bytes(b"unsigned-source")
    candidate_dir = tmp_path / "candidate"
    candidate_dir.mkdir()
    candidate = candidate_dir / source.name
    candidate.write_bytes(b"signed-notarized-candidate")
    candidate_sha = digest_file(candidate)
    release_version = "run-20260725-120000"
    rid = "osx-arm64"
    github = {
        "actor": "release-operator",
        "ref": "refs/heads/main",
        "repository": "ArchonMegalon/chummer6-ui",
        "sha": "1" * 40,
        "workflow": ".github/workflows/macos-flagship-evidence.yml",
    }
    authority_receipt = write_json(
        tmp_path / "authority-validation.json",
        {
            "candidateId": "candidate-20260725",
            "contractName": "chummer6-ui.macos-flagship-authority-validation",
            "contractVersion": 1,
            "generationId": "generation-20260725",
            "github": github,
            "predecessorHandoffSha256": "6" * 64,
            "predecessorSelectionAuthority": (
                "governance://global-flagship/n-minus-one/"
                "run-20260724-120000/to/run-20260725-120000/sha256/"
                + "6" * 64
            ),
            "releaseVersion": release_version,
            "rid": rid,
            "status": "pass",
        },
    )
    predecessor_verification = write_json(
        tmp_path / "predecessor-verification.json",
        {
            "artifact": {
                "fileName": "chummer-avalonia-osx-arm64-installer.dmg",
                "sha256": digest_bytes(b"signed-predecessor"),
                "sizeBytes": len(b"signed-predecessor"),
            },
            "contractName": "chummer6-ui.macos-predecessor-verification",
            "contractVersion": 1,
            "head": "avalonia",
            "handoffSha256": "6" * 64,
            "releaseVersion": "run-20260724-120000",
            "rid": rid,
            "status": "pass",
        },
    )
    predecessor_root = tmp_path / "predecessor"
    predecessor_root.mkdir()
    predecessor_artifact = (
        predecessor_root / "chummer-avalonia-osx-arm64-installer.dmg"
    )
    predecessor_artifact.write_bytes(b"signed-predecessor")
    stage_receipt = write_json(
        tmp_path / "mac-stage-only.json",
        {
            "appHeads": ["avalonia"],
            "contractName": "chummer.run.mac_release_stage_only",
            "countsAsPublicationEvidence": False,
            "mode": "stage_only",
            "outputPathDisclosure": "directory_name_only",
            "publicActivationAttempted": False,
            "publicationAttempted": False,
            "releaseVersion": release_version,
            "rid": rid,
            "status": "pass",
            "sourceReceiptSha256": "9" * 64,
            "uploadAttempted": False,
        },
    )
    stage_manifest = write_json(
        tmp_path / "RELEASE_CHANNEL.generated.json",
        {
            "artifacts": [
                {
                    "arch": "arm64",
                    "fileName": source.name,
                    "head": "avalonia",
                    "platform": "macos",
                    "rid": rid,
                    "sha256": digest_file(source),
                    "sizeBytes": source.stat().st_size,
                }
            ]
        },
    )
    signing_receipt = write_json(
        tmp_path / "signing.json",
        {
            "app": "avalonia",
            "artifacts": [
                {
                    "fileName": candidate.name,
                    "notarizationStatus": "pass",
                    "sha256": candidate_sha,
                    "signingStatus": "pass",
                }
            ],
            "contractName": "chummer6-ui.desktop_artifact_signing",
            "contractVersion": 2,
            "notarizationStatus": "pass",
            "platform": "macos",
            "releaseVersion": release_version,
            "rid": rid,
            "signingStatus": "pass",
            "status": "pass",
        },
    )
    notary_result = write_json(
        tmp_path / "notary-result.json",
        {
            "id": "01234567-89ab-cdef-0123-456789abcdef",
            "status": "Accepted",
        },
    )
    signing_identity_receipt = write_json(
        tmp_path / "signing-identity.json",
        {
            "artifact": {
                "fileName": candidate.name,
                "sha256": candidate_sha,
                "sizeBytes": candidate.stat().st_size,
            },
            "certificate": {
                "developerIdApplicationIdentity": (
                    "Developer ID Application: Example (ABCDE12345)"
                ),
                "sha256": "a" * 64,
                "spkiSha256": "b" * 64,
                "teamId": "ABCDE12345",
            },
            "contractName": (
                "chummer6-ui.macos-signing-notarization-identity.v1"
            ),
            "contractVersion": 1,
            "notarization": {
                "resultSha256": digest_file(notary_result),
                "status": "Accepted",
                "submissionId": "01234567-89ab-cdef-0123-456789abcdef",
            },
            "provenance": github,
            "releaseVersion": release_version,
            "rid": rid,
            "signingReceiptSha256": digest_file(signing_receipt),
            "sourceAuthorityReceiptSha256": digest_file(authority_receipt),
            "status": "pass",
        },
    )

    def startup(name: str) -> Path:
        return write_json(
            tmp_path / name,
            {
                "artifactDigest": "sha256:" + candidate_sha,
                "headId": "avalonia",
                "platform": "macos",
                "readyCheckpoint": "pre_ui_event_loop",
                "releaseVersion": release_version,
                "rid": rid,
                "status": "pass",
            },
        )

    pending = tmp_path / "pending-candidate.dmg"
    pending.write_bytes(candidate.read_bytes())
    manual_state = write_json(
        tmp_path / "manual-state.json",
        {
            "InstalledVersion": "run-20260724-120000",
            "LastFailureReason": "macos_manual_install_required",
            "ObservedStateSha256": "7" * 64,
            "PendingInstallerPath": pending.name,
            "PendingInstallerPathDisclosure": "file_name_only",
            "PendingUpdateVersion": release_version,
        },
    )
    pending_delivery = write_json(
        tmp_path / "pending-delivery.json",
        {
            "contractName": "chummer6-ui.macos-pending-installer-delivery",
            "contractVersion": 1,
            "pendingInstallerFileName": pending.name,
            "pendingInstallerSha256": candidate_sha,
            "pendingInstallerSizeBytes": candidate.stat().st_size,
            "releaseVersion": release_version,
            "stateSha256": digest_file(manual_state),
            "status": "pass",
        },
    )
    completed_state = write_json(
        tmp_path / "completed-state.json",
        {
            "InstalledVersion": release_version,
            "LastFailureReason": None,
            "ObservedStateSha256": "8" * 64,
            "PendingInstallerPath": None,
            "PendingUpdateVersion": None,
        },
    )
    observations = write_json(
        tmp_path / "observations.json",
        {
            "checks": {
                "candidateDmgCodesign": True,
                "candidateDmgGatekeeper": True,
                "candidateDmgStaple": True,
                "candidateHostArchitecture": True,
                "cleanInstallCopied": True,
                "coreStartup": True,
                "gatekeeperAssessmentsEnabled": True,
                "installedAppCodesign": True,
                "installedAppGatekeeper": True,
                "postUpdateStartup": True,
                "postUpdateUninstallRemoved": True,
                "predecessorAppGatekeeper": True,
                "predecessorUpdateStateObserved": True,
                "quarantineApplied": True,
                "uninstallRemoved": True,
                "updateCompletionStateObserved": True,
                "updateManualInstallCopied": True,
            },
            "contractName": "chummer6-ui.macos-flagship-runtime-observations",
            "contractVersion": 1,
            "releaseVersion": release_version,
            "rid": rid,
            "signingAuthority": {
                "identity": "Developer ID Application: Example (ABCDE12345)",
                "teamId": "ABCDE12345",
            },
        },
    )
    return {
        "authority": authority_receipt,
        "candidate": candidate,
        "clean_startup": startup("clean-startup.json"),
        "completed_state": completed_state,
        "inventory": tmp_path / "inventory.json",
        "manual_state": manual_state,
        "native_adapter": tmp_path / "native-adapter.json",
        "notary_result": notary_result,
        "observations": observations,
        "output": tmp_path / "evidence.json",
        "pending_delivery": pending_delivery,
        "post_update_startup": startup("post-update-startup.json"),
        "predecessor_artifact": predecessor_artifact,
        "predecessor_verification": predecessor_verification,
        "signing": signing_receipt,
        "signing_identity": signing_identity_receipt,
        "source": source,
        "stage_manifest": stage_manifest,
        "stage_receipt": stage_receipt,
    }


def collect_command(paths: dict[str, Path]) -> tuple[object, ...]:
    return (
        "collect",
        "--authority-receipt",
        paths["authority"],
        "--predecessor-verification",
        paths["predecessor_verification"],
        "--predecessor-artifact",
        paths["predecessor_artifact"],
        "--stage-receipt",
        paths["stage_receipt"],
        "--stage-manifest",
        paths["stage_manifest"],
        "--source-artifact",
        paths["source"],
        "--candidate-artifact",
        paths["candidate"],
        "--signing-receipt",
        paths["signing"],
        "--signing-identity-receipt",
        paths["signing_identity"],
        "--notary-result",
        paths["notary_result"],
        "--clean-startup-receipt",
        paths["clean_startup"],
        "--post-update-startup-receipt",
        paths["post_update_startup"],
        "--manual-update-state",
        paths["manual_state"],
        "--pending-delivery-receipt",
        paths["pending_delivery"],
        "--completed-update-state",
        paths["completed_state"],
        "--observations",
        paths["observations"],
        "--inventory-output",
        paths["inventory"],
        "--output",
        paths["output"],
        "--native-adapter-output",
        paths["native_adapter"],
        "--run-id",
        "100",
        "--run-attempt",
        "2",
        "--runner-os",
        "macos-15.6",
        "--runner-arch",
        "arm64",
    )


def aggregate_reference_files(
    paths: dict[str, Path], payload: dict
) -> dict[str, bytes]:
    sources = {
        "authorityReceipt": paths["authority"],
        "cleanStartupReceipt": paths["clean_startup"],
        "completedUpdateState": paths["completed_state"],
        "inventory": paths["inventory"],
        "manualUpdateState": paths["manual_state"],
        "notaryResult": paths["notary_result"],
        "pendingDeliveryReceipt": paths["pending_delivery"],
        "postUpdateStartupReceipt": paths["post_update_startup"],
        "predecessorVerification": paths["predecessor_verification"],
        "runtimeObservations": paths["observations"],
        "signingIdentityReceipt": paths["signing_identity"],
        "signingReceipt": paths["signing"],
        "stageManifest": paths["stage_manifest"],
        "stageOnlyReceipt": paths["stage_receipt"],
    }
    return {
        payload["references"][key]["path"]: path.read_bytes()
        for key, path in sources.items()
    }


def test_collect_emits_bound_nonpublishing_evidence(tmp_path: Path) -> None:
    paths = collect_fixture(tmp_path)

    result = run_tool(*collect_command(paths))

    assert result.returncode == 0, result.stderr
    receipt = json.loads(paths["output"].read_text(encoding="utf-8"))
    assert receipt["status"] == "pass"
    assert receipt["contractVersion"] == 2
    assert receipt["candidate"]["sha256"] == digest_file(paths["candidate"])
    assert receipt["updateDelivery"]["deliveryMode"] == (
        "macos_manual_installer_handoff"
    )
    assert receipt["updateDelivery"]["automaticApplySupported"] is False
    assert receipt["nonPublishing"]["publicationAttempted"] is False
    assert receipt["inventorySha256"] == digest_file(paths["inventory"])
    assert receipt["signing"] == {
        "candidateDmgGatekeeperStatus": "pass",
        "certificateSha256": "a" * 64,
        "certificateSpkiSha256": "b" * 64,
        "developerIdApplicationIdentity": (
            "Developer ID Application: Example (ABCDE12345)"
        ),
        "gatekeeperAssessmentsEnabled": True,
        "installedAppGatekeeperStatus": "pass",
        "notarizationStatus": "Accepted",
        "notarySubmissionId": "01234567-89ab-cdef-0123-456789abcdef",
        "postUpdateAppGatekeeperStatus": "pass",
        "staplerValidationStatus": "pass",
        "signingStatus": "pass",
        "teamId": "ABCDE12345",
    }
    assert set(receipt["references"]) == {
        "authorityReceipt",
        "cleanStartupReceipt",
        "completedUpdateState",
        "inventory",
        "manualUpdateState",
        "notaryResult",
        "pendingDeliveryReceipt",
        "postUpdateStartupReceipt",
        "predecessorVerification",
        "runtimeObservations",
        "signingIdentityReceipt",
        "signingReceipt",
        "stageManifest",
        "stageOnlyReceipt",
    }
    assert all(
        set(reference) == {"path", "sha256", "sizeBytes"}
        and reference["path"].startswith("receipts/")
        for reference in receipt["references"].values()
    )
    tool = load_tool_module()
    validated = tool.validate_aggregate_receipt(
        receipt,
        aggregate_reference_files(paths, receipt),
        expected_candidate=receipt["candidate"],
        expected_global_identity=receipt["globalCandidateIdentity"],
        expected_github=receipt["github"],
        expected_certificate_sha256="a" * 64,
        expected_certificate_spki_sha256="b" * 64,
        expected_developer_id_application_identity=(
            "Developer ID Application: Example (ABCDE12345)"
        ),
        expected_team_id="ABCDE12345",
    )
    assert validated["candidate"]["sha256"] == digest_file(
        paths["candidate"]
    )
    adapter = json.loads(
        paths["native_adapter"].read_text(encoding="utf-8")
    )
    assert (
        adapter["contractName"]
        == "chummer6-ui.flagship-native-e2e.macos.v1"
    )
    assert set(adapter) == {
        "artifact",
        "candidate",
        "checks",
        "contractName",
        "contractVersion",
        "generatedAt",
        "platform",
        "rid",
        "runner",
        "status",
    }
    assert set(adapter["checks"]) == {
        "cleanInstall",
        "coreWorkflow",
        "nMinusOneUpdate",
    }
    aggregate_reference = {
        "path": f"receipts/{paths['output'].name}",
        "sha256": digest_file(paths["output"]),
        "sizeBytes": paths["output"].stat().st_size,
    }
    assert all(
        check["evidence"] == aggregate_reference
        for check in adapter["checks"].values()
    )
    assert adapter["candidate"]["candidateId"] == "candidate-20260725"
    assert adapter["checks"]["nMinusOneUpdate"] == {
        "evidence": aggregate_reference,
        "fromReleaseVersion": "run-20260724-120000",
        "status": "pass",
        "toReleaseVersion": "run-20260725-120000",
    }


def test_aggregate_validator_rejects_tampering_and_authority_drift(
    tmp_path: Path,
) -> None:
    paths = collect_fixture(tmp_path)
    result = run_tool(*collect_command(paths))
    assert result.returncode == 0, result.stderr
    receipt = json.loads(paths["output"].read_text(encoding="utf-8"))
    reference_files = aggregate_reference_files(paths, receipt)
    tool = load_tool_module()

    signing_path = receipt["references"]["signingReceipt"]["path"]
    tampered_files = dict(reference_files)
    tampered_files[signing_path] = b'{"status":"pass"}'
    with pytest.raises(tool.ContractError, match="does not bind"):
        tool.validate_aggregate_receipt(receipt, tampered_files)

    rejected_notary_receipt = json.loads(json.dumps(receipt))
    rejected_notary_files = dict(reference_files)
    notary_path = receipt["references"]["notaryResult"]["path"]
    rejected_notary_raw = canonical(
        {
            "id": "01234567-89ab-cdef-0123-456789abcdef",
            "status": "Rejected",
        }
    )
    rejected_notary_files[notary_path] = rejected_notary_raw
    rejected_notary_receipt["references"]["notaryResult"].update(
        {
            "sha256": digest_bytes(rejected_notary_raw),
            "sizeBytes": len(rejected_notary_raw),
        }
    )
    rejected_notary_receipt["inputBindings"]["notaryResultSha256"] = (
        digest_bytes(rejected_notary_raw)
    )
    identity_path = receipt["references"]["signingIdentityReceipt"]["path"]
    rejected_identity = json.loads(reference_files[identity_path])
    rejected_identity["notarization"]["resultSha256"] = digest_bytes(
        rejected_notary_raw
    )
    rejected_identity_raw = canonical(rejected_identity)
    rejected_notary_files[identity_path] = rejected_identity_raw
    rejected_notary_receipt["references"]["signingIdentityReceipt"].update(
        {
            "sha256": digest_bytes(rejected_identity_raw),
            "sizeBytes": len(rejected_identity_raw),
        }
    )
    rejected_notary_receipt["inputBindings"][
        "signingIdentityReceiptSha256"
    ] = digest_bytes(rejected_identity_raw)
    with pytest.raises(tool.ContractError, match="accepted notary result"):
        tool.validate_aggregate_receipt(
            rejected_notary_receipt, rejected_notary_files
        )

    changed_posture = json.loads(json.dumps(receipt))
    changed_posture["nonPublishing"]["publicationAttempted"] = True
    with pytest.raises(tool.ContractError, match="not fail-closed"):
        tool.validate_aggregate_receipt(changed_posture, reference_files)

    with pytest.raises(tool.ContractError, match="certificate SHA-256"):
        tool.validate_aggregate_receipt(
            receipt,
            reference_files,
            expected_certificate_sha256="c" * 64,
        )
    with pytest.raises(tool.ContractError, match="Developer ID"):
        tool.validate_aggregate_receipt(
            receipt,
            reference_files,
            expected_developer_id_application_identity=(
                "Developer ID Application: Other (ABCDE12345)"
            ),
        )


def test_collect_rejects_publication_or_missing_e2e_check(
    tmp_path: Path,
) -> None:
    paths = collect_fixture(tmp_path)
    stage = json.loads(paths["stage_receipt"].read_text(encoding="utf-8"))
    stage["publicationAttempted"] = True
    write_json(paths["stage_receipt"], stage)
    result = run_tool(*collect_command(paths))
    assert result.returncode != 0
    assert "publicationAttempted must be false" in result.stderr

    paths = collect_fixture(tmp_path / "second")
    observed = json.loads(paths["observations"].read_text(encoding="utf-8"))
    observed["checks"]["candidateDmgStaple"] = False
    write_json(paths["observations"], observed)
    result = run_tool(*collect_command(paths))
    assert result.returncode != 0
    assert "candidateDmgStaple" in result.stderr

    paths = collect_fixture(tmp_path / "third")
    paths["predecessor_artifact"].write_bytes(b"drifted-predecessor")
    result = run_tool(*collect_command(paths))
    assert result.returncode != 0
    assert "installed N-1 DMG" in result.stderr


def escrow_fixture(paths: dict[str, Path]) -> tuple[Path, Path]:
    evidence = json.loads(paths["output"].read_text(encoding="utf-8"))
    ciphertext = paths["output"].parent / (
        "chummer-avalonia-osx-arm64-installer.dmg.aes256gcm"
    )
    ciphertext.write_bytes(b"e" * evidence["candidate"]["sizeBytes"])
    recipient_sha = "c" * 64
    producer = {
        "actor": "release-operator",
        "environment": "macos-flagship-evidence",
        "ref": "refs/heads/main",
        "repository": "ArchonMegalon/chummer6-ui",
        "runAttempt": "2",
        "runId": "100",
        "sha": "1" * 40,
        "workflow": ".github/workflows/macos-flagship-evidence.yml",
    }
    aad = {
        "candidate": evidence["candidate"],
        "candidateId": evidence["globalCandidateIdentity"]["candidateId"],
        "generationId": evidence["globalCandidateIdentity"]["generationId"],
        "producer": producer,
        "recipientSpkiSha256": recipient_sha,
        "releaseVersion": evidence["releaseVersion"],
        "rid": evidence["rid"],
    }
    aad_sha = digest_bytes(canonical(aad))
    oaep_label = (
        "chummer6-ui.macos-flagship-candidate-escrow.v1"
        + "\0"
        + aad_sha
    ).encode()
    receipt = {
        "aad": aad,
        "aadSha256": aad_sha,
        "candidate": evidence["candidate"],
        "ciphertext": {
            "fileName": ciphertext.name,
            "sha256": digest_file(ciphertext),
            "sizeBytes": ciphertext.stat().st_size,
        },
        "contractName": (
            "chummer6-ui.macos-flagship-candidate-escrow.v1"
        ),
        "contractVersion": 1,
        "encryption": {
            "authenticationTagBase64": base64.b64encode(b"t" * 16).decode(),
            "cipher": "aes-256-gcm",
            "keyWrap": "rsa-oaep-sha256",
            "nonceBase64": base64.b64encode(b"n" * 12).decode(),
            "oaepLabelSha256": digest_bytes(oaep_label),
            "wrappedKeyBase64": base64.b64encode(b"k" * 384).decode(),
        },
        "recipient": {
            "keyType": "rsa",
            "modulusBits": 3072,
            "publicExponent": 65537,
            "spkiSha256": recipient_sha,
        },
        "status": "sealed",
    }
    receipt_path = write_canonical(
        paths["output"].parent
        / "MACOS_FLAGSHIP_CANDIDATE_ESCROW.generated.json",
        receipt,
    )
    return receipt_path, ciphertext


def test_handoff_binds_exact_actions_artifact_and_run(tmp_path: Path) -> None:
    paths = collect_fixture(tmp_path)
    collected = run_tool(*collect_command(paths))
    assert collected.returncode == 0, collected.stderr
    escrow_receipt, escrow_ciphertext = escrow_fixture(paths)
    handoff = tmp_path / "handoff.json"
    command = (
        "emit-handoff",
        "--evidence",
        paths["output"],
        "--inventory",
        paths["inventory"],
        "--native-adapter",
        paths["native_adapter"],
        "--escrow-receipt",
        escrow_receipt,
        "--escrow-ciphertext",
        escrow_ciphertext,
        "--artifact-id",
        "300",
        "--artifact-digest",
        "a" * 64,
        "--artifact-name",
        "macos-flagship-encrypted-escrow-100-2",
        "--artifact-url",
        (
            "https://github.com/ArchonMegalon/chummer6-ui/actions/"
            "runs/100/artifacts/300"
        ),
        "--repository",
        "ArchonMegalon/chummer6-ui",
        "--ref",
        "refs/heads/main",
        "--sha",
        "1" * 40,
        "--actor",
        "release-operator",
        "--run-id",
        "100",
        "--run-attempt",
        "2",
        "--output",
        handoff,
    )

    result = run_tool(*command)

    assert result.returncode == 0, result.stderr
    payload = json.loads(handoff.read_text(encoding="utf-8"))
    assert payload["artifactName"] == "macos-flagship-encrypted-escrow-100-2"
    assert payload["artifactContents"] == (
        "receipts_and_encrypted_candidate_escrow"
    )
    assert payload["candidateBytesRetained"] is True
    assert payload["candidatePlaintextDistributed"] is False
    assert payload["environment"] == "macos-flagship-evidence"
    assert payload["candidateEscrow"]["ciphertextSha256"] == digest_file(
        escrow_ciphertext
    )
    assert payload["provenanceAuthenticated"] is False
    assert "GitHub API" in payload["requiredNextAuthority"]
    assert payload["evidenceSha256"] == digest_file(paths["output"])

    rejected = list(command)
    rejected[rejected.index("macos-flagship-encrypted-escrow-100-2")] = (
        "macos-flagship-encrypted-escrow-999-2"
    )
    result = run_tool(*rejected)
    assert result.returncode != 0
    assert "bound to run and attempt" in result.stderr

    escrow_ciphertext.write_bytes(escrow_ciphertext.read_bytes() + b"tamper")
    result = run_tool(*command)
    assert result.returncode != 0
    assert "ciphertext bytes do not match" in result.stderr

    escrow_receipt, _ = escrow_fixture(paths)
    forged = json.loads(escrow_receipt.read_text(encoding="utf-8"))
    forged["aad"]["producer"]["actor"] = "attacker"
    forged["aadSha256"] = digest_bytes(canonical(forged["aad"]))
    forged["encryption"]["oaepLabelSha256"] = digest_bytes(
        (
            "chummer6-ui.macos-flagship-candidate-escrow.v1"
            + "\0"
            + forged["aadSha256"]
        ).encode()
    )
    write_canonical(escrow_receipt, forged)
    result = run_tool(*command)
    assert result.returncode != 0
    assert "producer does not match GitHub runtime" in result.stderr


def test_workflow_is_pinned_fail_closed_and_nonpublishing() -> None:
    text = WORKFLOW.read_text(encoding="utf-8")

    assert "self-hosted" in text
    assert "ARM64" in text
    assert "chummer-macos-flagship-" in text
    assert "ephemeral self-hosted Apple Silicon runner" in text
    assert "environment: macos-flagship-evidence" in text
    assert "--stage-only" in text
    assert "CHUMMER_MACOS_DEVELOPER_ID_P12_BASE64" in text
    assert "CHUMMER_MACOS_NOTARY_KEY_P8_BASE64" in text
    assert "CHUMMER_MACOS_DEVELOPER_ID_APPLICATION" in text
    assert "CHUMMER_MACOS_TEAM_ID" in text
    assert "CHUMMER_MACOS_CERT_SHA256" in text
    assert "CHUMMER_MACOS_CERT_SPKI_SHA256" in text
    assert "CHUMMER_MACOS_ESCROW_RECIPIENT_PUBLIC_KEY_PEM_BASE64" in text
    assert "CHUMMER_MACOS_ESCROW_RECIPIENT_SPKI_SHA256" in text
    assert "macos_flagship_candidate_escrow.mjs verify-recipient" in text
    assert "macos_flagship_candidate_escrow.mjs seal" in text
    assert "macos-flagship-encrypted-escrow-" in text
    assert "chummer-avalonia-osx-arm64-installer.dmg.aes256gcm" in text
    assert "--escrow-receipt" in text
    assert "--escrow-ciphertext" in text
    assert "security delete-keychain" in text
    assert 'test "$(spctl --status)" = "assessments enabled"' in text
    assert (
        'CHUMMER_HUB_LOCAL_PROOF_MUTATION_LOCK_PATH="$mutation_lock"'
        in text
    )
    assert "$BUILD_ROOT/proof-locks/hub-local-proof-mutation.lock" in text
    assert "run-macos-flagship-evidence.sh" in text
    assert "AUTHORITY_ROOT: ${{ runner.temp }}" not in text
    assert "${{ env.EVIDENCE_ROOT }}/files" not in text
    assert "${{ runner.temp }}/macos-flagship-evidence/files" not in text
    assert (
        text.index("Build the governed unsigned source bundle")
        < text.index("Prepare an ephemeral Developer ID")
    )
    assert "No release upload, publication, Registry mutation" in text
    assert "contents: write" not in text
    assert "actions: write" not in text
    assert "publish-download-bundle" not in text
    uses = re.findall(r"^\s*uses:\s*[^@\s]+@([0-9a-f]+)\s*$", text, re.MULTILINE)
    assert uses
    assert all(len(commit) == 40 for commit in uses)


def test_workflow_bootstrap_source_is_exact_hub_commit_contract() -> None:
    text = WORKFLOW.read_text(encoding="utf-8")

    assert "repository: ArchonMegalon/chummer6-hub" in text
    assert "chummer.run-services" not in text
    assert "hub_commit: ${{ steps.authority.outputs.hub_commit }}" in text
    assert "print(f\"hub_commit={payload['hubCommit']}\")" in text
    assert "ref: ${{ needs.preflight.outputs.hub_commit }}" in text
    assert "services_commit" not in text
    assert (
        'test "$(git -C .release-authority rev-parse HEAD)" = '
        '"$CHUMMER_HUB_EXPECTED_COMMIT"'
    ) in text
    assert (
        "test -f .release-authority/scripts/run-mac-release-bootstrap.sh"
        in text
    )
    assert (
        "test ! -L .release-authority/scripts/run-mac-release-bootstrap.sh"
        in text
    )


def test_native_runner_contract_has_full_install_and_update_denominator() -> None:
    syntax = subprocess.run(
        ("bash", "-n", str(RUNNER)),
        cwd=REPO_ROOT,
        check=False,
        capture_output=True,
        text=True,
    )
    assert syntax.returncode == 0, syntax.stderr
    text = RUNNER.read_text(encoding="utf-8")
    for required in (
        "xattr -w com.apple.quarantine",
        "spctl --assess --type execute",
        "spctl --assess --type open",
        "Gatekeeper assessments must be enabled",
        "codesign --verify --deep --strict",
        "lipo -archs",
        "does not contain arm64",
        "--startup-smoke",
        "macos_manual_install_required",
        "pending_receipt_path",
        '"stateSha256": hashlib.sha256(projected_raw).hexdigest()',
        "safe_remove_isolated_app",
        "non-publishing lane rejects",
    ):
        assert required in text
    for mode in ("manual", "completed"):
        assert re.search(
            r'wait_for_update_state\s*\\?\s*'
            r'"\$update_state_path"\s*\\?\s*'
            rf'"{mode}"',
            text,
        )
    assert text.index(
        "could not destroy Apple signing authority before runtime execution"
    ) < text.index('candidate_mount="$RUN_ROOT/candidate-mount"')


def test_installer_builder_emits_bound_json_notary_result() -> None:
    text = INSTALLER_BUILDER.read_text(encoding="utf-8")
    for required in (
        "CHUMMER_MAC_NOTARY_RESULT_PATH",
        "--output-format json",
        'payload.get("status") != "Accepted"',
        "notarytool result did not prove an accepted submission",
    ):
        assert required in text


def test_runbook_documents_authority_secrets_and_first_predecessor_blocker() -> None:
    text = RUNBOOK.read_text(encoding="utf-8")
    for required in (
        ".github/workflows/macos-flagship-evidence.yml",
        "macos-flagship-evidence",
        "CHUMMER_MACOS_DEVELOPER_ID_P12_BASE64",
        "CHUMMER_MACOS_NOTARY_KEY_P8_BASE64",
        "chummer-macos-flagship-<nonce>",
        "macos_manual_install_required",
        "Until a signed, notarized public macOS predecessor exists",
        "cannot stage a public generation",
        "CHUMMER_MACOS_ESCROW_RECIPIENT_PUBLIC_KEY_PEM_BASE64",
        "candidateBytesRetained: true",
    ):
        assert required in text


def test_python_contract_compiles() -> None:
    result = subprocess.run(
        (sys.executable, "-m", "py_compile", str(TOOL)),
        cwd=REPO_ROOT,
        check=False,
        capture_output=True,
        text=True,
    )
    assert result.returncode == 0, result.stderr
    node = subprocess.run(
        ("node", "--check", str(ESCROW_TOOL)),
        cwd=REPO_ROOT,
        check=False,
        capture_output=True,
        text=True,
    )
    assert node.returncode == 0, node.stderr
