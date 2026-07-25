from __future__ import annotations

import ast
import hashlib
import importlib.util
import json
import sys
from pathlib import Path
from types import ModuleType

import pytest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "release" / "assemble_global_flagship_release.py"
NOW = "2026-07-25T12:00:00Z"
SOURCE_COMMIT = "a" * 40


def load_module() -> ModuleType:
    spec = importlib.util.spec_from_file_location(
        "assemble_global_flagship_release", SCRIPT
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


ASSEMBLER = load_module()


@pytest.fixture(autouse=True)
def fixed_production_clock(monkeypatch: pytest.MonkeyPatch) -> None:
    fixed = ASSEMBLER.parse_time(NOW, "test clock")
    monkeypatch.setattr(ASSEMBLER, "current_time", lambda: fixed)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def reference(root: Path, path: Path) -> dict[str, object]:
    return {
        "path": path.relative_to(root).as_posix(),
        "sha256": sha256(path),
        "sizeBytes": path.stat().st_size,
    }


def artifact_reference(
    root: Path, path: Path, artifact_id: str
) -> dict[str, object]:
    return {
        "artifactId": artifact_id,
        "fileName": path.name,
        **reference(root, path),
    }


def candidate_identity() -> dict[str, str]:
    return {
        "candidateId": "candidate-20260725",
        "generationId": "generation-20260725",
        "releaseVersion": "run-20260725",
        "previousReleaseVersion": "run-20260724",
        "sourceCommit": SOURCE_COMMIT,
    }


def make_fixture(tmp_path: Path) -> tuple[Path, dict[str, Path]]:
    root = tmp_path / "candidate"
    root.mkdir()
    paths: dict[str, Path] = {}
    platform_data = {
        "windows": {
            "rid": "win-x64",
            "fileName": "chummer-avalonia-win-x64-installer.exe",
            "artifactId": "avalonia-win-x64-installer",
            "runner": "windows-runner",
            "os": "windows-2025",
            "arch": "x64",
            "exitContract": "chummer6-ui.windows_desktop_exit_gate",
            "nativeContract": "chummer6-ui.flagship-native-e2e.windows.v1",
        },
        "linux": {
            "rid": "linux-x64",
            "fileName": "chummer-avalonia-linux-x64-installer.deb",
            "artifactId": "avalonia-linux-x64-installer",
            "runner": "linux-runner",
            "os": "linux-ubuntu-24.04",
            "arch": "x64",
            "exitContract": "chummer6-ui.linux_desktop_exit_gate",
            "nativeContract": "chummer6-ui.flagship-native-e2e.linux.v1",
        },
        "macos": {
            "rid": "osx-arm64",
            "fileName": "chummer-avalonia-osx-arm64-installer.dmg",
            "artifactId": "avalonia-osx-arm64-installer",
            "runner": "macos-runner",
            "os": "macos-15",
            "arch": "arm64",
            "exitContract": "chummer6-ui.macos_desktop_exit_gate",
            "nativeContract": "chummer6-ui.flagship-native-e2e.macos.v1",
        },
    }
    candidate_platforms: dict[str, object] = {}
    for platform, data in platform_data.items():
        artifact_path = root / "artifacts" / str(data["fileName"])
        artifact_path.parent.mkdir(parents=True, exist_ok=True)
        artifact_path.write_bytes(f"{platform}-immutable-artifact".encode())
        paths[f"{platform}_artifact"] = artifact_path
        artifact_sha = sha256(artifact_path)
        artifact_size = artifact_path.stat().st_size

        exit_path = root / "receipts" / f"{platform}-exit.json"
        if platform == "macos":
            exit_artifact = {
                "installer_sha256": artifact_sha,
                "installer_size_bytes": artifact_size,
            }
            exit_payload = {
                "contract_name": data["exitContract"],
                "generated_at": "2026-07-25T11:40:00Z",
                "channelId": "stable",
                "releaseVersion": "run-20260725",
                "status": "passed",
                "head": {
                    "app_key": "avalonia",
                    "platform": platform,
                    "rid": data["rid"],
                },
                "artifact": exit_artifact,
            }
        else:
            exit_payload = {
                "contract_name": data["exitContract"],
                "generated_at": "2026-07-25T11:40:00Z",
                "channelId": "stable",
                "releaseVersion": "run-20260725",
                "status": "passed",
                "head": {
                    "app_key": "avalonia",
                    "platform": platform,
                    "rid": data["rid"],
                },
                "checks": {
                    f"release_channel_{platform}_artifact": {
                        "sha256": artifact_sha,
                        "sizeBytes": artifact_size,
                    }
                },
            }
        write_json(exit_path, exit_payload)
        paths[f"{platform}_exit"] = exit_path

        signing_ref: dict[str, object] | None = None
        if platform in {"windows", "macos"}:
            signing_path = root / "receipts" / f"{platform}-signing.json"
            signing_payload = {
                "contractName": "chummer6-ui.desktop_artifact_signing",
                "contractVersion": 2,
                "generatedAt": "2026-07-25T11:42:00Z",
                "platform": platform,
                "app": "avalonia",
                "rid": data["rid"],
                "releaseChannel": "stable",
                "releaseVersion": "run-20260725",
                "signingStatus": "pass",
                "notarizationStatus": "pass" if platform == "macos" else None,
                "artifacts": [
                    {
                        "fileName": data["fileName"],
                        "sha256": artifact_sha,
                        "signingStatus": "pass",
                        "notarizationStatus": (
                            "pass" if platform == "macos" else None
                        ),
                    }
                ],
            }
            write_json(signing_path, signing_payload)
            paths[f"{platform}_signing"] = signing_path
            signing_ref = reference(root, signing_path)

        check_payloads: dict[str, object] = {}
        for check_name in ("clean", "core", "update"):
            evidence_path = (
                root / "native-evidence" / platform / f"{check_name}.receipt"
            )
            evidence_path.parent.mkdir(parents=True, exist_ok=True)
            evidence_path.write_text(
                f"{platform}:{check_name}:passed\n", encoding="utf-8"
            )
            paths[f"{platform}_{check_name}"] = evidence_path
            check_payloads[check_name] = reference(root, evidence_path)

        native_path = root / "receipts" / f"{platform}-native-e2e.json"
        native_payload = {
            "contractName": data["nativeContract"],
            "contractVersion": 1,
            "generatedAt": "2026-07-25T11:45:00Z",
            "status": "passed",
            "candidate": candidate_identity(),
            "platform": platform,
            "rid": data["rid"],
            "artifact": {
                "artifactId": data["artifactId"],
                "fileName": data["fileName"],
                "sha256": artifact_sha,
                "sizeBytes": artifact_size,
            },
            "runner": {
                "repository": "ArchonMegalon/chummer6-ui",
                "workflow": f".github/workflows/{platform}-native-e2e.yml",
                "ref": "refs/heads/main",
                "runId": 100,
                "runAttempt": 1,
                "actor": data["runner"],
                "os": data["os"],
                "arch": data["arch"],
            },
            "checks": {
                "cleanInstall": {
                    "status": "passed",
                    "mode": "clean",
                    "evidence": check_payloads["clean"],
                },
                "coreWorkflow": {
                    "status": "passed",
                    "scenario": "create-save-close-reopen-export",
                    "evidence": check_payloads["core"],
                },
                "nMinusOneUpdate": {
                    "status": "passed",
                    "fromReleaseVersion": "run-20260724",
                    "toReleaseVersion": "run-20260725",
                    "evidence": check_payloads["update"],
                },
            },
        }
        write_json(native_path, native_payload)
        paths[f"{platform}_native"] = native_path

        candidate_platforms[platform] = {
            "rid": data["rid"],
            "artifact": artifact_reference(
                root, artifact_path, str(data["artifactId"])
            ),
            "exitGateReceipt": reference(root, exit_path),
            "signingReceipt": signing_ref,
            "nativeE2eReceipt": reference(root, native_path),
        }

    candidate_path = root / "GLOBAL_FLAGSHIP_CANDIDATE.generated.json"
    candidate = {
        "contractName": "chummer6-ui.global-flagship-candidate.v1",
        "contractVersion": 1,
        "generatedAt": "2026-07-25T11:30:00Z",
        "expiresAt": "2026-07-26T11:30:00Z",
        "candidateId": "candidate-20260725",
        "generationId": "generation-20260725",
        "releaseVersion": "run-20260725",
        "previousReleaseVersion": "run-20260724",
        "channelId": "stable",
        "source": {
            "repository": "ArchonMegalon/chummer6-ui",
            "ref": "refs/heads/main",
            "commit": SOURCE_COMMIT,
        },
        "producer": {
            "actor": "candidate-producer",
            "workflow": ".github/workflows/global-flagship-candidate.yml",
            "runId": 50,
            "runAttempt": 1,
        },
        "platforms": candidate_platforms,
    }
    write_json(candidate_path, candidate)
    paths["candidate"] = candidate_path
    return candidate_path, paths


def refresh_candidate_reference(candidate_path: Path, key: str, path: Path) -> None:
    candidate = json.loads(candidate_path.read_text(encoding="utf-8"))
    root = candidate_path.parent
    candidate["platforms"][key.split("_")[0]][key.split("_", 1)[1]] = reference(
        root, path
    )
    write_json(candidate_path, candidate)


def run_propose(candidate: Path, output: Path) -> int:
    return ASSEMBLER.main(
        [
            "propose",
            "--candidate",
            str(candidate),
            "--output",
            str(output),
        ]
    )


def approval_payload(
    proposal: Path, role: str, actor: str, *, run_id: int
) -> dict[str, object]:
    proposal_payload = json.loads(proposal.read_text(encoding="utf-8"))
    return {
        "contractName": "chummer6-ui.global-flagship-release-approval.v1",
        "contractVersion": 1,
        "proposalSha256": sha256(proposal),
        "proposalSizeBytes": proposal.stat().st_size,
        "candidateId": proposal_payload["candidate"]["candidateId"],
        "generationId": proposal_payload["candidate"]["generationId"],
        "role": role,
        "decision": "approve",
        "approvedAt": "2026-07-25T12:05:00Z",
        "expiresAt": "2026-07-25T15:00:00Z",
        "actor": actor,
        "authority": {
            "repository": "ArchonMegalon/chummer6-ui",
            "workflow": ".github/workflows/global-flagship-release-approval.yml",
            "ref": "refs/heads/main",
            "runId": run_id,
            "runAttempt": 1,
            "environment": "global-flagship-release-review",
        },
    }


def make_approvals(tmp_path: Path, proposal: Path) -> list[Path]:
    approvals = []
    for index, (role, actor) in enumerate(
        (
            ("quality", "quality-reviewer"),
            ("release", "release-reviewer"),
            ("security", "security-reviewer"),
        ),
        start=1,
    ):
        path = tmp_path / f"{role}-approval.json"
        write_json(path, approval_payload(proposal, role, actor, run_id=200 + index))
        approvals.append(path)
    return approvals


def test_propose_and_finalize_bind_three_platforms_without_publication(
    tmp_path: Path,
) -> None:
    candidate, _ = make_fixture(tmp_path)
    proposal = tmp_path / "proposal.json"
    assert run_propose(candidate, proposal) == 0

    proposed = json.loads(proposal.read_text(encoding="utf-8"))
    assert proposed["status"] == "ready_for_independent_approval"
    assert set(proposed["platforms"]) == {"windows", "linux", "macos"}
    assert proposed["nonPublishing"] is True
    assert proposed["publicationAuthorized"] is False
    assert proposed["authorityLevel"] == "local-structural-validation-only"
    assert proposed["provenanceAuthenticated"] is False
    assert proposed["allowedSideEffects"] == ["write_local_receipts"]
    assert proposed["platforms"]["linux"]["signingReceipt"] is None
    assert (
        proposed["platforms"]["macos"]["integrityPolicy"]
        == "developer-id-signed-notarized-stapled-and-manifest-sha256"
    )

    approvals = make_approvals(tmp_path, proposal)
    final_receipt = tmp_path / "final.json"
    argv = [
        "finalize",
        "--proposal",
        str(proposal),
        "--candidate",
        str(candidate),
        "--output",
        str(final_receipt),
    ]
    for path in approvals:
        argv.extend(["--approval", str(path)])
    assert ASSEMBLER.main(argv) == 0

    final = json.loads(final_receipt.read_text(encoding="utf-8"))
    assert final["status"] == "passed"
    assert [item["role"] for item in final["approvals"]] == [
        "quality",
        "release",
        "security",
    ]
    assert final["proposal"]["sha256"] == sha256(proposal)
    assert final["nonPublishing"] is True
    assert final["publicationAuthorized"] is False
    assert final["authorityLevel"] == "local-structural-validation-only"
    assert final["provenanceAuthenticated"] is False
    assert final["handoff"]["eligibleForSeparatePublicationReview"] is True
    assert "provider API" in final["handoff"]["requiredNextAuthority"]


def test_missing_artifact_fails_closed_and_surfaces_external_requirements(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    paths["macos_artifact"].unlink()
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    receipt = json.loads(output.read_text(encoding="utf-8"))
    assert receipt["status"] == "blocked"
    assert receipt["publicationAuthorized"] is False
    assert "macOS arm64 runner" in json.dumps(receipt["externalRequirements"])
    assert "missing" in receipt["blockers"][0]


def test_stale_platform_evidence_fails_closed(tmp_path: Path) -> None:
    candidate, paths = make_fixture(tmp_path)
    exit_path = paths["linux_exit"]
    payload = json.loads(exit_path.read_text(encoding="utf-8"))
    payload["generated_at"] = "2026-07-23T11:40:00Z"
    write_json(exit_path, payload)
    refresh_candidate_reference(candidate, "linux_exitGateReceipt", exit_path)
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "stale" in blocker
    assert "linux exit-gate" in blocker


def test_exit_gate_artifact_mismatch_fails_closed(tmp_path: Path) -> None:
    candidate, paths = make_fixture(tmp_path)
    exit_path = paths["windows_exit"]
    payload = json.loads(exit_path.read_text(encoding="utf-8"))
    payload["checks"]["release_channel_windows_artifact"]["sha256"] = "f" * 64
    write_json(exit_path, payload)
    refresh_candidate_reference(candidate, "windows_exitGateReceipt", exit_path)
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "artifact SHA-256" in blocker


def test_n_minus_one_update_must_match_both_release_versions(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    native_path = paths["macos_native"]
    payload = json.loads(native_path.read_text(encoding="utf-8"))
    payload["checks"]["nMinusOneUpdate"]["fromReleaseVersion"] = "run-wrong"
    write_json(native_path, payload)
    refresh_candidate_reference(candidate, "macos_nativeE2eReceipt", native_path)
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "N-1 source version" in blocker


def test_signing_and_notarization_are_required_for_macos(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    signing_path = paths["macos_signing"]
    payload = json.loads(signing_path.read_text(encoding="utf-8"))
    payload["notarizationStatus"] = "fail"
    write_json(signing_path, payload)
    refresh_candidate_reference(candidate, "macos_signingReceipt", signing_path)
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "notarization and stapling" in blocker


def test_finalization_rejects_non_independent_approval_actor(
    tmp_path: Path,
) -> None:
    candidate, _ = make_fixture(tmp_path)
    proposal = tmp_path / "proposal.json"
    assert run_propose(candidate, proposal) == 0
    approvals = make_approvals(tmp_path, proposal)
    payload = approval_payload(
        proposal, "quality", "candidate-producer", run_id=999
    )
    write_json(approvals[0], payload)
    final_receipt = tmp_path / "final.json"
    argv = [
        "finalize",
        "--proposal",
        str(proposal),
        "--candidate",
        str(candidate),
        "--output",
        str(final_receipt),
    ]
    for path in approvals:
        argv.extend(["--approval", str(path)])

    assert ASSEMBLER.main(argv) == 1
    blocker = json.loads(final_receipt.read_text(encoding="utf-8"))["blockers"][0]
    assert "not independent" in blocker


def test_finalization_requires_distinct_role_actors(tmp_path: Path) -> None:
    candidate, _ = make_fixture(tmp_path)
    proposal = tmp_path / "proposal.json"
    assert run_propose(candidate, proposal) == 0
    approvals = make_approvals(tmp_path, proposal)
    release = approval_payload(
        proposal, "release", "quality-reviewer", run_id=777
    )
    write_json(approvals[1], release)
    final_receipt = tmp_path / "final.json"
    argv = [
        "finalize",
        "--proposal",
        str(proposal),
        "--candidate",
        str(candidate),
        "--output",
        str(final_receipt),
    ]
    for path in approvals:
        argv.extend(["--approval", str(path)])

    assert ASSEMBLER.main(argv) == 1
    blocker = json.loads(final_receipt.read_text(encoding="utf-8"))["blockers"][0]
    assert "distinct actors" in blocker


def test_finalization_revalidates_candidate_artifact_bytes(tmp_path: Path) -> None:
    candidate, paths = make_fixture(tmp_path)
    proposal = tmp_path / "proposal.json"
    assert run_propose(candidate, proposal) == 0
    approvals = make_approvals(tmp_path, proposal)
    paths["windows_artifact"].write_bytes(b"mutated-after-proposal")
    final_receipt = tmp_path / "final.json"
    argv = [
        "finalize",
        "--proposal",
        str(proposal),
        "--candidate",
        str(candidate),
        "--output",
        str(final_receipt),
    ]
    for path in approvals:
        argv.extend(["--approval", str(path)])

    assert ASSEMBLER.main(argv) == 1
    blocker = json.loads(final_receipt.read_text(encoding="utf-8"))["blockers"][0]
    assert "digest or size" in blocker


def test_candidate_references_must_not_traverse_symlinks(tmp_path: Path) -> None:
    candidate, paths = make_fixture(tmp_path)
    real_artifact = paths["linux_artifact"]
    linked_directory = candidate.parent / "artifacts-link"
    linked_directory.symlink_to(real_artifact.parent.name)
    link = linked_directory / real_artifact.name
    payload = json.loads(candidate.read_text(encoding="utf-8"))
    payload["platforms"]["linux"]["artifact"] = {
        "artifactId": "avalonia-linux-x64-installer",
        "fileName": real_artifact.name,
        **reference(candidate.parent, real_artifact),
        "path": link.relative_to(candidate.parent).as_posix(),
    }
    write_json(candidate, payload)
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "symlink" in blocker


def test_assembler_has_no_network_or_process_execution_imports() -> None:
    tree = ast.parse(SCRIPT.read_text(encoding="utf-8"))
    imported_roots: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            imported_roots.update(alias.name.split(".", 1)[0] for alias in node.names)
        elif isinstance(node, ast.ImportFrom) and node.module:
            imported_roots.add(node.module.split(".", 1)[0])
    assert imported_roots.isdisjoint(
        {"requests", "urllib", "http", "socket", "subprocess"}
    )


def test_cli_does_not_expose_freshness_bypass_overrides(
    tmp_path: Path,
) -> None:
    candidate, _ = make_fixture(tmp_path)
    output = tmp_path / "proposal.json"

    with pytest.raises(SystemExit):
        ASSEMBLER.main(
            [
                "propose",
                "--candidate",
                str(candidate),
                "--output",
                str(output),
                "--now",
                NOW,
            ]
        )
    with pytest.raises(SystemExit):
        ASSEMBLER.main(
            [
                "propose",
                "--candidate",
                str(candidate),
                "--output",
                str(output),
                "--max-evidence-age-seconds",
                str(ASSEMBLER.MAX_EVIDENCE_AGE_SECONDS),
            ]
        )
    assert not output.exists()


def test_output_must_not_alias_an_authority_input(tmp_path: Path) -> None:
    candidate, _ = make_fixture(tmp_path)
    candidate_before = candidate.read_bytes()
    assert run_propose(candidate, candidate) == 2
    assert candidate.read_bytes() == candidate_before

    alias = tmp_path / "candidate-hard-link.json"
    alias.hardlink_to(candidate)
    assert run_propose(candidate, alias) == 2
    assert candidate.read_bytes() == candidate_before

    proposal = tmp_path / "proposal.json"
    assert run_propose(candidate, proposal) == 0
    approvals = make_approvals(tmp_path, proposal)
    proposal_before = proposal.read_bytes()
    argv = [
        "finalize",
        "--proposal",
        str(proposal),
        "--candidate",
        str(candidate),
        "--output",
        str(proposal),
    ]
    for path in approvals:
        argv.extend(["--approval", str(path)])
    assert ASSEMBLER.main(argv) == 2
    assert proposal.read_bytes() == proposal_before


@pytest.mark.parametrize(
    "platform,receipt_key",
    [
        ("windows", "signingReceipt"),
        ("macos", "signingReceipt"),
        ("windows", "nativeE2eReceipt"),
        ("linux", "nativeE2eReceipt"),
        ("macos", "nativeE2eReceipt"),
    ],
)
def test_missing_required_platform_receipt_fails_closed(
    tmp_path: Path, platform: str, receipt_key: str
) -> None:
    candidate, _ = make_fixture(tmp_path)
    payload = json.loads(candidate.read_text(encoding="utf-8"))
    payload["platforms"][platform][receipt_key] = None
    write_json(candidate, payload)
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    assert json.loads(output.read_text(encoding="utf-8"))["status"] == "blocked"
