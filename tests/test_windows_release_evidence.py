from __future__ import annotations

import hashlib
import json
import subprocess
from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
VERIFY_SCRIPT = REPO_ROOT / "scripts" / "verify-windows-release-evidence.py"


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def fixture(
    root: Path,
    *,
    signing_status: str = "pass",
    execution_environment: str = "native_windows",
    signing_version: str = "run-windows-test",
    include_windows: bool = True,
    include_proof_route: bool = True,
) -> dict[str, Path]:
    version = "run-windows-test"
    channel = "preview"
    artifact_id = "avalonia-win-x64-installer"
    file_name = "chummer-avalonia-win-x64-installer.exe"
    installer_bytes = b"MZ" + (b"current-windows-installer" * 256)
    digest = hashlib.sha256(installer_bytes).hexdigest()
    files_dir = root / "files"
    files_dir.mkdir(parents=True)
    (files_dir / file_name).write_bytes(installer_bytes)

    artifact = {
        "artifactId": artifact_id,
        "id": artifact_id,
        "head": "avalonia",
        "platform": "windows",
        "rid": "win-x64",
        "arch": "x64",
        "kind": "installer",
        "fileName": file_name,
        "sha256": digest,
        "sizeBytes": len(installer_bytes),
    }
    routes = [f"/downloads/install/{artifact_id}"] if include_proof_route else []
    manifest = {
        "version": version,
        "channelId": channel,
        "artifacts": [artifact] if include_windows else [],
        "releaseProof": {"proofRoutes": routes},
    }
    downloads = {
        "version": version,
        "downloads": [artifact] if include_windows else [],
    }

    release_channel_path = root / "RELEASE_CHANNEL.generated.json"
    downloads_path = root / "releases.json"
    write_json(release_channel_path, manifest)
    write_json(downloads_path, downloads)

    signing_dir = root / "signing"
    write_json(
        signing_dir / "signing-avalonia-win-x64.receipt.json",
        {
            "contractName": "chummer6-ui.desktop_artifact_signing",
            "platform": "windows",
            "app": "avalonia",
            "rid": "win-x64",
            "releaseChannel": channel,
            "releaseVersion": signing_version,
            "signingStatus": signing_status,
            "artifacts": [
                {
                    "fileName": file_name,
                    "sha256": digest,
                    "signingStatus": signing_status,
                }
            ],
        },
    )

    startup_dir = root / "startup-smoke"
    write_json(
        startup_dir / "startup-smoke-avalonia-win-x64.receipt.json",
        {
            "status": "pass",
            "readyCheckpoint": "pre_ui_event_loop",
            "headId": "avalonia",
            "platform": "windows",
            "rid": "win-x64",
            "channelId": channel,
            "releaseVersion": version,
            "artifactFileName": file_name,
            "artifactDigest": f"sha256:{digest}",
            "executionEnvironment": execution_environment,
        },
    )

    exit_gate_path = root / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
    write_json(
        exit_gate_path,
        {
            "contract_name": "chummer6-ui.windows_desktop_exit_gate",
            "status": "passed",
            "blockingMode": "none",
            "releaseVersion": version,
            "head": {"app_key": "avalonia", "rid": "win-x64"},
            "checks": {
                "installer_sha256": digest,
                "startup_smoke_artifact_digest": f"sha256:{digest}",
                "windows_installer_visual_effective_artifact_digest": f"sha256:{digest}",
                "windows_installer_visual_proof_skipped": False,
            },
        },
    )
    return {
        "release_channel": release_channel_path,
        "downloads": downloads_path,
        "files": files_dir,
        "signing": signing_dir,
        "startup": startup_dir,
        "exit_gate": exit_gate_path,
        "handoff": root / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json",
    }


def configure_proof_only_visual_handoff(paths: dict[str, Path]) -> None:
    manifest = json.loads(paths["release_channel"].read_text(encoding="utf-8"))
    manifest["supportabilityState"] = "review_required"
    manifest["publicTrustMetrics"] = {
        "releaseChannel": {
            "channelId": "preview",
            "supportabilityState": "review_required",
        }
    }
    manifest["registryBoundaryCoverage"] = {
        "channelId": "preview",
        "releaseChannel": {
            "supportabilityState": "review_required",
            "publicTrustPosture": "blocked",
        }
    }
    write_json(paths["release_channel"], manifest)

    artifact = manifest["artifacts"][0]
    version = manifest["version"]
    file_name = artifact["fileName"]
    digest = artifact["sha256"]
    startup_path = paths["startup"] / "startup-smoke-avalonia-win-x64.receipt.json"
    startup = json.loads(startup_path.read_text(encoding="utf-8"))
    visual_reason = (
        "Windows installer visual proof is missing; capture progress and completion screenshots "
        "on a Windows host."
    )

    exit_gate = json.loads(paths["exit_gate"].read_text(encoding="utf-8"))
    exit_gate["status"] = "failed"
    exit_gate["blockingMode"] = "external_only"
    exit_gate["blocking_mode"] = "external_only"
    exit_gate["reasons"] = [visual_reason]
    exit_gate["checks"]["windows_installer_visual_effective_artifact_digest"] = ""
    exit_gate["checks"]["windows_installer_visual_proof_skipped"] = False
    write_json(paths["exit_gate"], exit_gate)

    write_json(
        paths["handoff"],
        {
            "contract_name": "chummer6-ui.windows_installer_visual_proof_handoff",
            "handoff_only": True,
            "handoff_scope": "staged_nightly_windows_visual_proof",
            "stable_release_unchanged": True,
            "requires_separate_publish_lane": True,
            "status": "ready_for_windows_host",
            "only_blocker_is_visual_proof": True,
            "blockers": [],
            "release": {
                "channel_id": "preview",
                "version": version,
                "release_version": version,
            },
            "windows_installer": {
                "artifact_id": artifact["artifactId"],
                "file_name": file_name,
                "sha256": f"sha256:{digest}",
            },
            "startup_smoke_path": str(startup_path),
            "startup_smoke": {
                "status": startup["status"],
                "version": version,
                "release_version": version,
                "receipt_file_name": startup_path.name,
                "receipt_sha256": hashlib.sha256(startup_path.read_bytes()).hexdigest(),
                "artifact_file_name": file_name,
                "artifact_digest": f"sha256:{digest}",
                "matches_release_version": True,
                "matches_artifact_file_name": True,
                "matches_artifact_digest": True,
            },
            "windows_gate_status": "failed",
            "windows_gate_reasons": [visual_reason],
        },
    )


def run_verifier(paths: dict[str, Path], *extra: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            "python3",
            str(VERIFY_SCRIPT),
            "--release-channel",
            str(paths["release_channel"]),
            "--downloads-manifest",
            str(paths["downloads"]),
            "--files-dir",
            str(paths["files"]),
            "--signing-dir",
            str(paths["signing"]),
            "--startup-smoke-dir",
            str(paths["startup"]),
            "--windows-exit-gate",
            str(paths["exit_gate"]),
            *extra,
        ],
        text=True,
        capture_output=True,
        check=False,
    )


def test_signed_native_windows_evidence_is_flagship_ready(tmp_path: Path) -> None:
    result = run_verifier(
        fixture(tmp_path),
        "--require-authenticode",
        "--require-native-windows",
    )

    assert result.returncode == 0, result.stderr
    payload = json.loads(result.stdout)
    assert payload["status"] == "pass"
    assert payload["verdict"] == "WINDOWS_FLAGSHIP_READY"
    assert payload["launchReady"] is True
    assert payload["supportabilityFloor"] == "preview_supported"


def test_unsigned_wine_preview_is_proof_only_and_review_gated(tmp_path: Path) -> None:
    result = run_verifier(
        fixture(
            tmp_path,
            signing_status="skipped_preview",
            execution_environment="wine_compatibility",
        )
    )

    assert result.returncode == 0, result.stderr
    payload = json.loads(result.stdout)
    assert payload["status"] == "proof_only"
    assert payload["verdict"] == "WINDOWS_PROOF_PREVIEW_READY"
    assert payload["launchReady"] is False
    assert payload["supportabilityFloor"] == "review_required"
    assert "unsigned preview artifact" in " ".join(payload["caveats"])
    assert "native Windows execution proof is outstanding" in " ".join(payload["caveats"])


def test_flagship_mode_rejects_unsigned_wine_receipts(tmp_path: Path) -> None:
    result = run_verifier(
        fixture(
            tmp_path,
            signing_status="skipped_preview",
            execution_environment="wine_compatibility",
        ),
        "--require-authenticode",
        "--require-native-windows",
    )

    assert result.returncode == 1
    payload = json.loads(result.stderr)
    assert "Authenticode signing receipt is not passing" in " ".join(payload["errors"])
    assert "executionEnvironment=native_windows" in " ".join(payload["errors"])


def test_stale_signing_receipt_cannot_validate_current_installer(tmp_path: Path) -> None:
    result = run_verifier(fixture(tmp_path, signing_version="run-old"))

    assert result.returncode == 1
    payload = json.loads(result.stderr)
    assert "signing version mismatch" in " ".join(payload["errors"])


def test_manifest_without_windows_or_proof_route_fails_closed(tmp_path: Path) -> None:
    no_windows = run_verifier(fixture(tmp_path / "no-windows", include_windows=False))
    missing_route = run_verifier(fixture(tmp_path / "missing-route", include_proof_route=False))

    assert no_windows.returncode == 1
    assert "contains no Windows install artifact" in no_windows.stderr
    assert missing_route.returncode == 1
    assert "release proof is missing /downloads/install/avalonia-win-x64-installer" in missing_route.stderr


def test_forced_preview_can_use_digest_bound_visual_handoff_as_proof_only(tmp_path: Path) -> None:
    paths = fixture(
        tmp_path,
        signing_status="skipped_preview",
        execution_environment="wine_compatibility",
    )
    configure_proof_only_visual_handoff(paths)

    result = run_verifier(
        paths,
        "--windows-visual-proof-handoff",
        str(paths["handoff"]),
        "--allow-proof-only-visual-handoff",
    )

    assert result.returncode == 0, result.stderr
    payload = json.loads(result.stdout)
    assert payload["status"] == "proof_only"
    assert payload["verdict"] == "WINDOWS_PROOF_PREVIEW_READY"
    assert payload["launchReady"] is False
    assert payload["supportabilityFloor"] == "review_required"
    assert payload["allowProofOnlyVisualHandoff"] is True
    assert payload["checkedArtifacts"][0]["proofOnlyVisualHandoff"] is True
    assert "native Windows installer visual proof is outstanding" in " ".join(payload["caveats"])


def test_visual_handoff_requires_explicit_opt_in_and_explicit_path(tmp_path: Path) -> None:
    paths = fixture(tmp_path)
    configure_proof_only_visual_handoff(paths)

    path_without_opt_in = run_verifier(
        paths,
        "--windows-visual-proof-handoff",
        str(paths["handoff"]),
    )
    opt_in_without_path = run_verifier(paths, "--allow-proof-only-visual-handoff")

    assert path_without_opt_in.returncode == 1
    assert "requires explicit --allow-proof-only-visual-handoff" in path_without_opt_in.stderr
    assert opt_in_without_path.returncode == 1
    assert "requires --windows-visual-proof-handoff" in opt_in_without_path.stderr


def test_visual_handoff_rejects_stale_receipt_binding_and_other_gate_blockers(
    tmp_path: Path,
) -> None:
    paths = fixture(tmp_path)
    configure_proof_only_visual_handoff(paths)

    handoff = json.loads(paths["handoff"].read_text(encoding="utf-8"))
    handoff["startup_smoke"]["receipt_sha256"] = "0" * 64
    write_json(paths["handoff"], handoff)
    exit_gate = json.loads(paths["exit_gate"].read_text(encoding="utf-8"))
    exit_gate["reasons"].append("Windows startup smoke receipt status is not passing.")
    write_json(paths["exit_gate"], exit_gate)

    result = run_verifier(
        paths,
        "--windows-visual-proof-handoff",
        str(paths["handoff"]),
        "--allow-proof-only-visual-handoff",
    )

    assert result.returncode == 1
    payload = json.loads(result.stderr)
    errors = " ".join(payload["errors"])
    assert "startup-smoke receipt digest mismatch" in errors
    assert "contains a non-visual-proof blocker" in errors
    assert "reasons do not match the staged exit gate" in errors


def test_visual_handoff_rejects_relaxed_public_posture(tmp_path: Path) -> None:
    paths = fixture(tmp_path)
    configure_proof_only_visual_handoff(paths)
    manifest = json.loads(paths["release_channel"].read_text(encoding="utf-8"))
    manifest["supportabilityState"] = "preview_supported"
    manifest["registryBoundaryCoverage"]["releaseChannel"]["publicTrustPosture"] = "preview"
    write_json(paths["release_channel"], manifest)

    result = run_verifier(
        paths,
        "--windows-visual-proof-handoff",
        str(paths["handoff"]),
        "--allow-proof-only-visual-handoff",
    )

    assert result.returncode == 1
    assert "top-level supportabilityState=review_required" in result.stderr
    assert "registry publicTrustPosture=blocked" in result.stderr


def test_visual_handoff_cannot_relax_stable_release_requirements(tmp_path: Path) -> None:
    paths = fixture(tmp_path)
    configure_proof_only_visual_handoff(paths)
    manifest = json.loads(paths["release_channel"].read_text(encoding="utf-8"))
    manifest["channelId"] = "public_stable"
    write_json(paths["release_channel"], manifest)

    result = run_verifier(
        paths,
        "--windows-visual-proof-handoff",
        str(paths["handoff"]),
        "--allow-proof-only-visual-handoff",
        "--require-authenticode",
        "--require-native-windows",
    )

    assert result.returncode == 1
    payload = json.loads(result.stderr)
    assert payload["launchReady"] is False
    assert "allowed only for channel preview" in " ".join(payload["errors"])
