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
DESKTOP_FIXTURE_SCRIPT = ROOT / "tests" / "test_desktop_native_lifecycle_evidence.py"
MACOS_FIXTURE_SCRIPT = ROOT / "tests" / "test_macos_flagship_evidence.py"
NOW = "2026-07-25T12:00:00Z"
SOURCE_COMMIT = "1" * 40


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

DESKTOP_FIXTURE_SPEC = importlib.util.spec_from_file_location(
    "_global_flagship_desktop_fixture", DESKTOP_FIXTURE_SCRIPT
)
assert (
    DESKTOP_FIXTURE_SPEC is not None
    and DESKTOP_FIXTURE_SPEC.loader is not None
)
DESKTOP_FIXTURES = importlib.util.module_from_spec(DESKTOP_FIXTURE_SPEC)
sys.modules[DESKTOP_FIXTURE_SPEC.name] = DESKTOP_FIXTURES
DESKTOP_FIXTURE_SPEC.loader.exec_module(DESKTOP_FIXTURES)

MACOS_FIXTURE_SPEC = importlib.util.spec_from_file_location(
    "_global_flagship_macos_fixture", MACOS_FIXTURE_SCRIPT
)
assert (
    MACOS_FIXTURE_SPEC is not None
    and MACOS_FIXTURE_SPEC.loader is not None
)
MACOS_FIXTURES = importlib.util.module_from_spec(MACOS_FIXTURE_SPEC)
sys.modules[MACOS_FIXTURE_SPEC.name] = MACOS_FIXTURES
MACOS_FIXTURE_SPEC.loader.exec_module(MACOS_FIXTURES)


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
        "releaseVersion": "run-20260725-120000",
        "previousReleaseVersion": "run-20260724-120000",
        "sourceCommit": SOURCE_COMMIT,
    }


def rewrite_bound_json(
    root: Path,
    binding: dict[str, object],
    mutate: object,
) -> None:
    path = root / str(binding["path"])
    payload = json.loads(path.read_text(encoding="utf-8"))
    assert callable(mutate)
    mutate(payload)
    write_json(path, payload)
    binding["sha256"] = sha256(path)
    binding["sizeBytes"] = path.stat().st_size


def make_desktop_lifecycle(
    candidate_root: Path,
    platform: str,
    artifact_path: Path,
) -> tuple[Path, Path | None, Path]:
    evidence_root = candidate_root / "native-evidence" / platform
    evidence_root.mkdir(parents=True)
    if platform == "windows":
        receipt_path, receipt = DESKTOP_FIXTURES.passing_windows_receipt(
            evidence_root
        )
        rid = "win-x64"
        workflow = ".github/workflows/windows-native-evidence-capture.yml"
    else:
        receipt_path, receipt = DESKTOP_FIXTURES.passing_receipt(evidence_root)
        rid = "linux-x64"
        workflow = ".github/workflows/linux-native-lifecycle-evidence.yml"

    candidate = receipt["candidate"]
    previous = receipt["nMinusOne"]
    source = receipt["nativeRunner"]["source"]
    candidate.update(
        {
            "artifactFileName": artifact_path.name,
            "sha256": sha256(artifact_path),
            "sizeBytes": artifact_path.stat().st_size,
            "sourceCommit": SOURCE_COMMIT,
            "version": "run-20260725-120000",
        }
    )
    previous["version"] = "run-20260724-120000"
    source.update(
        {
            "actor": "github-actions[bot]",
            "ref": "refs/heads/main",
            "repository": "ArchonMegalon/chummer6-ui",
            "runAttempt": "1",
            "runId": "100",
            "sha": SOURCE_COMMIT,
            "workflow": workflow,
        }
    )
    receipt["generatedAt"] = "2026-07-25T06:00:00Z"

    for release_key, artifact in (
        ("candidate", candidate),
        ("nMinusOne", previous),
    ):
        for kind in ("startupReceipt", "mouseFirstReceipt"):
            binding = receipt["coreWorkflow"][release_key][kind]

            def mutate_core(
                payload: dict[str, object],
                *,
                expected_artifact: dict[str, object] = artifact,
            ) -> None:
                payload["artifactDigest"] = (
                    f"sha256:{expected_artifact['sha256']}"
                )
                payload["releaseVersion"] = expected_artifact["version"]
                payload["version"] = expected_artifact["version"]

            rewrite_bound_json(evidence_root, binding, mutate_core)

    manifest_binding = receipt["packageAuthority"]["manifestReceipt"]
    manifest_path = evidence_root / str(manifest_binding["path"])
    previous_binding = ASSEMBLER.desktop_lifecycle.receipt_n_minus_one_binding(
        previous, platform, rid
    )
    DESKTOP_FIXTURES.write_n_minus_one_manifest(
        manifest_path, previous_binding
    )
    previous["manifestSha256"] = previous_binding["manifestSha256"]
    manifest_binding["sha256"] = previous_binding["manifestSha256"]
    manifest_binding["sizeBytes"] = manifest_path.stat().st_size
    if platform == "linux":
        receipt["packageAuthority"]["manifestSha256"] = previous[
            "manifestSha256"
        ]
        live_binding = receipt["livePredecessorAuthority"][
            "liveReleaseChannel"
        ]
        live_path = evidence_root / str(live_binding["path"])
        live_raw = json.dumps(
            DESKTOP_FIXTURES.release_channel_manifest(previous_binding)
        )
        live_path.write_text(live_raw, encoding="utf-8")
        live_predecessor = (
            ASSEMBLER.desktop_lifecycle.validate_live_predecessor_authority(
                DESKTOP_FIXTURES.canonical(previous_binding),
                live_raw,
                "linux",
                "linux-x64",
            )
        )
        live_binding["sha256"] = sha256(live_path)
        live_binding["sizeBytes"] = live_path.stat().st_size
        receipt["livePredecessorAuthority"].update(
            {
                "liveReleaseChannelSha256": live_predecessor[
                    "liveReleaseChannelSha256"
                ],
                "nMinusOneReleaseSha256": live_predecessor[
                    "nMinusOneReleaseSha256"
                ],
                "selectedTupleSha256": live_predecessor[
                    "selectedTupleSha256"
                ],
            }
        )

    signing_path: Path | None = None
    if platform == "windows":
        for binding, artifact in (
            (
                receipt["packageAuthority"]["candidate"][
                    "authenticodeReceipt"
                ],
                candidate,
            ),
            (
                receipt["packageAuthority"]["nMinusOne"][
                    "authenticodeReceipt"
                ],
                previous,
            ),
        ):

            def mutate_authenticode(
                payload: dict[str, object],
                *,
                expected_artifact: dict[str, object] = artifact,
            ) -> None:
                payload["artifact"] = {
                    "fileName": expected_artifact["artifactFileName"],
                    "sha256": expected_artifact["sha256"],
                    "sizeBytes": expected_artifact["sizeBytes"],
                }
                payload["source"] = dict(source)

            rewrite_bound_json(
                evidence_root, binding, mutate_authenticode
            )

        signing_binding = receipt["packageAuthority"]["candidate"][
            "signingReceipt"
        ]
        signing_path = evidence_root / str(signing_binding["path"])

        def mutate_signing(payload: dict[str, object]) -> None:
            payload["app"] = "avalonia"
            payload["generatedAt"] = "2026-07-25T06:02:00Z"
            payload["releaseVersion"] = candidate["version"]
            payload["artifactSignatures"][0]["artifactFileName"] = candidate[
                "artifactFileName"
            ]
            payload["artifactSignatures"][0]["artifactSha256"] = candidate[
                "sha256"
            ]
            payload["artifacts"][0]["fileName"] = candidate[
                "artifactFileName"
            ]
            payload["artifacts"][0]["sha256"] = candidate["sha256"]

        rewrite_bound_json(evidence_root, signing_binding, mutate_signing)

    write_json(receipt_path, receipt)
    adapter_path = candidate_root / "receipts" / f"{platform}-native-e2e.json"
    ASSEMBLER.desktop_lifecycle.emit_flagship_adapter(
        receipt_path=receipt_path,
        evidence_root=evidence_root,
        candidate_root=candidate_root,
        evidence_path=receipt_path.relative_to(candidate_root).as_posix(),
        output_path=adapter_path,
        candidate_id="candidate-20260725",
        generation_id="generation-20260725",
        artifact_id=f"avalonia-{rid}-installer",
        source_commit=SOURCE_COMMIT,
    )
    return adapter_path, signing_path, receipt_path


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
            "runner": "release-operator",
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
        macos_paths: dict[str, Path] | None = None
        if platform == "macos":
            macos_paths = MACOS_FIXTURES.collect_fixture(root / "receipts")
            artifact_path.write_bytes(macos_paths["candidate"].read_bytes())

            signing_payload = json.loads(
                macos_paths["signing"].read_text(encoding="utf-8")
            )
            signing_payload["generatedAt"] = "2026-07-25T11:42:00Z"
            write_json(macos_paths["signing"], signing_payload)
            signing_identity = json.loads(
                macos_paths["signing_identity"].read_text(encoding="utf-8")
            )
            signing_identity["signingReceiptSha256"] = sha256(
                macos_paths["signing"]
            )
            write_json(macos_paths["signing_identity"], signing_identity)

            collect_result = MACOS_FIXTURES.run_tool(
                *MACOS_FIXTURES.collect_command(macos_paths)
            )
            assert collect_result.returncode == 0, collect_result.stderr

            aggregate_payload = json.loads(
                macos_paths["output"].read_text(encoding="utf-8")
            )
            aggregate_payload["generatedAtUtc"] = "2026-07-25T11:44:00Z"
            write_json(macos_paths["output"], aggregate_payload)
            adapter_payload = json.loads(
                macos_paths["native_adapter"].read_text(encoding="utf-8")
            )
            adapter_payload["generatedAt"] = "2026-07-25T11:45:00Z"
            aggregate_ref = reference(root, macos_paths["output"])
            for check in adapter_payload["checks"].values():
                check["evidence"] = dict(aggregate_ref)
            write_json(macos_paths["native_adapter"], adapter_payload)
            paths["macos_aggregate"] = macos_paths["output"]
            paths["macos_notary_result"] = macos_paths["notary_result"]
            paths["macos_signing_identity"] = macos_paths[
                "signing_identity"
            ]

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
                "releaseVersion": "run-20260725-120000",
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
                "releaseVersion": "run-20260725-120000",
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
        if platform == "macos":
            assert macos_paths is not None
            signing_path = macos_paths["signing"]
            paths[f"{platform}_signing"] = signing_path
            signing_ref = reference(root, signing_path)

        if platform in {"windows", "linux"}:
            native_path, desktop_signing, lifecycle_path = make_desktop_lifecycle(
                root, platform, artifact_path
            )
            paths[f"{platform}_lifecycle"] = lifecycle_path
            if platform == "windows":
                assert desktop_signing is not None
                paths["windows_signing"] = desktop_signing
                signing_ref = reference(root, desktop_signing)
        else:
            assert macos_paths is not None
            native_path = macos_paths["native_adapter"]
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
        "releaseVersion": "run-20260725-120000",
        "previousReleaseVersion": "run-20260724-120000",
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


def refresh_desktop_lifecycle_adapter(
    candidate_path: Path,
    paths: dict[str, Path],
    platform: str,
) -> None:
    root = candidate_path.parent
    lifecycle_path = paths[f"{platform}_lifecycle"]
    adapter_path = paths[f"{platform}_native"]
    adapter = json.loads(adapter_path.read_text(encoding="utf-8"))
    lifecycle_reference = reference(root, lifecycle_path)
    for check in ("cleanInstall", "coreWorkflow", "nMinusOneUpdate"):
        adapter["checks"][check]["evidence"] = lifecycle_reference
    write_json(adapter_path, adapter)
    refresh_candidate_reference(
        candidate_path, f"{platform}_nativeE2eReceipt", adapter_path
    )


def refresh_macos_aggregate_adapter(
    candidate_path: Path, paths: dict[str, Path]
) -> None:
    root = candidate_path.parent
    adapter_path = paths["macos_native"]
    adapter = json.loads(adapter_path.read_text(encoding="utf-8"))
    aggregate_reference = reference(root, paths["macos_aggregate"])
    for check in ("cleanInstall", "coreWorkflow", "nMinusOneUpdate"):
        adapter["checks"][check]["evidence"] = dict(aggregate_reference)
    write_json(adapter_path, adapter)
    refresh_candidate_reference(
        candidate_path, "macos_nativeE2eReceipt", adapter_path
    )


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


def make_reviewer_policy(tmp_path: Path) -> Path:
    path = tmp_path / "reviewer-policy.json"
    write_json(
        path,
        {
            "contractName": (
                "chummer6-ui.global-flagship-release-reviewer-policy.v1"
            ),
            "contractVersion": 1,
            "roles": {
                "quality": ["quality-reviewer"],
                "release": ["release-reviewer"],
                "security": ["security-reviewer"],
            },
        },
    )
    return path


def approval_argv(
    proposal: Path,
    policy: Path,
    output: Path,
    *,
    role: str = "quality",
    actor: str = "quality-reviewer",
    triggering_actor: str | None = None,
) -> list[str]:
    return [
        "approve",
        "--proposal",
        str(proposal),
        "--reviewer-policy",
        str(policy),
        "--expected-proposal-sha256",
        sha256(proposal),
        "--role",
        role,
        "--approval-confirmed",
        "true",
        "--repository",
        "ArchonMegalon/chummer6-ui",
        "--ref",
        "refs/heads/main",
        "--sha",
        SOURCE_COMMIT,
        "--workflow-ref",
        (
            "ArchonMegalon/chummer6-ui/"
            ".github/workflows/global-flagship-release-approval.yml"
            "@refs/heads/main"
        ),
        "--workflow-sha",
        SOURCE_COMMIT,
        "--run-id",
        "301",
        "--run-attempt",
        "1",
        "--actor",
        actor,
        "--triggering-actor",
        triggering_actor or actor,
        "--environment-approver",
        (
            "release-reviewer"
            if actor != "release-reviewer"
            else "security-reviewer"
        ),
        "--environment",
        "global-flagship-release-review",
        "--output",
        str(output),
    ]


def approval_payload(
    proposal: Path, role: str, actor: str, *, run_id: int
) -> dict[str, object]:
    proposal_payload = json.loads(proposal.read_text(encoding="utf-8"))
    environment_approvers = {
        "quality": "release-reviewer",
        "release": "security-reviewer",
        "security": "quality-reviewer",
    }
    return {
        "contractName": "chummer6-ui.global-flagship-release-approval.v2",
        "contractVersion": 2,
        "proposalSha256": sha256(proposal),
        "proposalSizeBytes": proposal.stat().st_size,
        "candidateId": proposal_payload["candidate"]["candidateId"],
        "generationId": proposal_payload["candidate"]["generationId"],
        "role": role,
        "decision": "approve",
        "approvalConfirmed": True,
        "approvedAt": "2026-07-25T12:05:00Z",
        "expiresAt": "2026-07-25T15:00:00Z",
        "actor": actor,
        "triggeringActor": actor,
        "rerunPolicy": "fresh-dispatch-only",
        "environmentApproval": {
            "state": "approved",
            "reviewer": environment_approvers[role],
        },
        "reviewerPolicy": {
            "contractName": (
                "chummer6-ui.global-flagship-release-reviewer-policy.v1"
            ),
            "sha256": "2" * 64,
            "sizeBytes": 42,
            "role": role,
            "actorAuthorized": True,
            "rolesDisjoint": True,
            "authorizedRoleMembers": 1,
        },
        "authority": {
            "repository": "ArchonMegalon/chummer6-ui",
            "workflow": ".github/workflows/global-flagship-release-approval.yml",
            "ref": "refs/heads/main",
            "sha": SOURCE_COMMIT,
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
    candidate, paths = make_fixture(tmp_path)
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
    macos_requirement = next(
        item["requirement"]
        for item in proposed["externalRequirements"]
        if item["platform"] == "macos"
    )
    assert "pinned encrypted escrow" in macos_requirement
    assert "provider API" in macos_requirement
    assert proposed["platforms"]["linux"]["signingReceipt"] is None
    assert proposed["platforms"]["windows"]["signingReceipt"][
        "signingBackend"
    ] == "digicert_keylocker_linux_jsign"
    assert proposed["platforms"]["windows"]["signingReceipt"][
        "signerCertificateSha256"
    ] == "8" * 64
    assert proposed["platforms"]["windows"]["signingReceipt"][
        "signerSpkiSha256"
    ] == "9" * 64
    assert proposed["platforms"]["windows"]["nativeLifecycleEvidence"][
        "candidateSigningReceiptSha256"
    ] == sha256(paths["windows_signing"])
    assert proposed["platforms"]["windows"]["nativeLifecycleEvidence"][
        "source"
    ] == {
        "repository": "ArchonMegalon/chummer6-ui",
        "workflow": ".github/workflows/windows-native-evidence-capture.yml",
        "ref": "refs/heads/main",
        "commit": SOURCE_COMMIT,
            "runId": 100,
            "runAttempt": 1,
            "actor": "github-actions[bot]",
            "triggeringActor": "github-actions[bot]",
            "rerunPolicy": "same-actor-only",
        }
    assert (
        proposed["platforms"]["macos"]["integrityPolicy"]
        == "developer-id-signed-notarized-stapled-and-manifest-sha256"
    )
    macos_evidence = proposed["platforms"]["macos"][
        "nativeLifecycleEvidence"
    ]
    assert macos_evidence["contractName"] == (
        "chummer6-ui.macos-flagship-evidence"
    )
    assert macos_evidence["contractVersion"] == 3
    assert macos_evidence["aggregateSha256"] == sha256(
        paths["macos_aggregate"]
    )
    assert macos_evidence["certificateSha256"] == "a" * 64
    assert macos_evidence["certificateSpkiSha256"] == "b" * 64
    assert macos_evidence["developerIdApplicationIdentity"] == (
        "Developer ID Application: Example (ABCDE12345)"
    )
    assert macos_evidence["teamId"] == "ABCDE12345"
    assert (
        macos_evidence["references"]["signingReceipt"]["sha256"]
        == sha256(paths["macos_signing"])
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


def test_approve_emits_exact_proposal_bound_protected_workflow_receipt(
    tmp_path: Path,
) -> None:
    candidate, _ = make_fixture(tmp_path)
    proposal = tmp_path / "proposal.json"
    policy = make_reviewer_policy(tmp_path)
    output = tmp_path / "approval.json"
    assert run_propose(candidate, proposal) == 0
    assert proposal.stat().st_size <= 45_000

    assert ASSEMBLER.main(approval_argv(proposal, policy, output)) == 0

    receipt = json.loads(output.read_text(encoding="utf-8"))
    assert receipt["contractName"] == (
        "chummer6-ui.global-flagship-release-approval.v2"
    )
    assert receipt["contractVersion"] == 2
    assert receipt["proposalSha256"] == sha256(proposal)
    assert receipt["proposalSizeBytes"] == proposal.stat().st_size
    assert receipt["role"] == "quality"
    assert receipt["decision"] == "approve"
    assert receipt["approvalConfirmed"] is True
    assert receipt["actor"] == "quality-reviewer"
    assert receipt["triggeringActor"] == "quality-reviewer"
    assert receipt["rerunPolicy"] == "fresh-dispatch-only"
    assert receipt["environmentApproval"] == {
        "state": "approved",
        "reviewer": "release-reviewer",
    }
    assert receipt["reviewerPolicy"] == {
        "contractName": (
            "chummer6-ui.global-flagship-release-reviewer-policy.v1"
        ),
        "sha256": sha256(policy),
        "sizeBytes": policy.stat().st_size,
        "role": "quality",
        "actorAuthorized": True,
        "rolesDisjoint": True,
        "authorizedRoleMembers": 1,
    }
    assert receipt["authority"]["sha"] == SOURCE_COMMIT
    assert receipt["authority"]["workflow"] == (
        ".github/workflows/global-flagship-release-approval.yml"
    )


def test_approve_rejects_rerun_by_a_different_triggering_actor(
    tmp_path: Path,
) -> None:
    candidate, _ = make_fixture(tmp_path)
    proposal = tmp_path / "proposal.json"
    policy = make_reviewer_policy(tmp_path)
    output = tmp_path / "approval.json"
    assert run_propose(candidate, proposal) == 0

    assert (
        ASSEMBLER.main(
            approval_argv(
                proposal,
                policy,
                output,
                triggering_actor="release-reviewer",
            )
        )
        == 1
    )
    blocked = json.loads(output.read_text(encoding="utf-8"))
    assert blocked["contractVersion"] == 2
    assert blocked["status"] == "blocked"
    assert "same-actor rerun policy" in blocked["blockers"][0]


def test_approve_requires_fresh_dispatch_and_distinct_environment_approver(
    tmp_path: Path,
) -> None:
    candidate, _ = make_fixture(tmp_path)
    proposal = tmp_path / "proposal.json"
    policy = make_reviewer_policy(tmp_path)
    assert run_propose(candidate, proposal) == 0

    rerun_output = tmp_path / "rerun-approval.json"
    rerun_args = approval_argv(proposal, policy, rerun_output)
    attempt_index = rerun_args.index("--run-attempt") + 1
    rerun_args[attempt_index] = "2"
    assert ASSEMBLER.main(rerun_args) == 1
    assert "fresh-dispatch runAttempt" in json.loads(
        rerun_output.read_text(encoding="utf-8")
    )["blockers"][0]

    same_actor_output = tmp_path / "same-actor-environment.json"
    same_actor_args = approval_argv(
        proposal, policy, same_actor_output
    )
    approver_index = same_actor_args.index("--environment-approver") + 1
    same_actor_args[approver_index] = "quality-reviewer"
    assert ASSEMBLER.main(same_actor_args) == 1
    assert "environment approver must be independent" in json.loads(
        same_actor_output.read_text(encoding="utf-8")
    )["blockers"][0]


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (
            lambda policy: policy["roles"]["release"].append(
                "quality-reviewer"
            ),
            "appears in both",
        ),
        (
            lambda policy: policy["roles"]["quality"].clear(),
            "between 1 and",
        ),
        (
            lambda policy: policy["roles"]["quality"].__setitem__(
                0, "different-reviewer"
            ),
            "not authorized",
        ),
    ],
)
def test_approve_rejects_invalid_or_unauthorized_reviewer_policy(
    tmp_path: Path,
    mutate: object,
    message: str,
) -> None:
    candidate, _ = make_fixture(tmp_path)
    proposal = tmp_path / "proposal.json"
    policy_path = make_reviewer_policy(tmp_path)
    policy = json.loads(policy_path.read_text(encoding="utf-8"))
    assert callable(mutate)
    mutate(policy)
    write_json(policy_path, policy)
    output = tmp_path / "approval.json"
    assert run_propose(candidate, proposal) == 0

    assert ASSEMBLER.main(approval_argv(proposal, policy_path, output)) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert message in blocker


def test_approve_requires_exact_proposal_hash_and_candidate_source_sha(
    tmp_path: Path,
) -> None:
    candidate, _ = make_fixture(tmp_path)
    proposal = tmp_path / "proposal.json"
    policy = make_reviewer_policy(tmp_path)
    assert run_propose(candidate, proposal) == 0

    wrong_digest_output = tmp_path / "wrong-digest.json"
    wrong_digest_args = approval_argv(
        proposal, policy, wrong_digest_output
    )
    digest_index = wrong_digest_args.index(
        "--expected-proposal-sha256"
    ) + 1
    wrong_digest_args[digest_index] = "f" * 64
    assert ASSEMBLER.main(wrong_digest_args) == 1
    assert "expected proposal SHA-256" in json.loads(
        wrong_digest_output.read_text(encoding="utf-8")
    )["blockers"][0]

    wrong_sha_output = tmp_path / "wrong-sha.json"
    wrong_sha_args = approval_argv(proposal, policy, wrong_sha_output)
    sha_index = wrong_sha_args.index("--sha") + 1
    wrong_sha_args[sha_index] = "3" * 40
    workflow_sha_index = wrong_sha_args.index("--workflow-sha") + 1
    wrong_sha_args[workflow_sha_index] = "3" * 40
    assert ASSEMBLER.main(wrong_sha_args) == 1
    assert "candidate source commit" in json.loads(
        wrong_sha_output.read_text(encoding="utf-8")
    )["blockers"][0]


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


def test_adapter_pass_booleans_cannot_replace_rich_lifecycle_validation(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    lifecycle_path = paths["linux_lifecycle"]
    lifecycle = json.loads(lifecycle_path.read_text(encoding="utf-8"))
    lifecycle["phases"][3]["details"]["statePreserved"] = False
    write_json(lifecycle_path, lifecycle)
    refresh_desktop_lifecycle_adapter(candidate, paths, "linux")
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "rich lifecycle receipt failed independent validation" in blocker
    assert "statePreserved" in blocker


def test_desktop_adapter_checks_must_reference_one_exact_lifecycle_receipt(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    adapter_path = paths["linux_native"]
    adapter = json.loads(adapter_path.read_text(encoding="utf-8"))
    lifecycle_copy = (
        candidate.parent / "native-evidence" / "linux" / "lifecycle-copy.json"
    )
    lifecycle_copy.write_bytes(paths["linux_lifecycle"].read_bytes())
    adapter["checks"]["coreWorkflow"]["evidence"] = reference(
        candidate.parent, lifecycle_copy
    )
    write_json(adapter_path, adapter)
    refresh_candidate_reference(
        candidate, "linux_nativeE2eReceipt", adapter_path
    )
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "does not equal the shared rich lifecycle receipt" in blocker


def test_rich_lifecycle_runner_identity_is_cross_bound_to_adapter(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    lifecycle_path = paths["linux_lifecycle"]
    lifecycle = json.loads(lifecycle_path.read_text(encoding="utf-8"))
    lifecycle["nativeRunner"]["source"]["runId"] = "101"
    write_json(lifecycle_path, lifecycle)
    refresh_desktop_lifecycle_adapter(candidate, paths, "linux")
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "rich lifecycle receipt.source.runId" in blocker


def test_adapter_rejects_different_rerun_triggering_actor(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    adapter_path = paths["linux_native"]
    adapter = json.loads(adapter_path.read_text(encoding="utf-8"))
    adapter["runner"]["triggeringActor"] = "human-operator"
    write_json(adapter_path, adapter)
    refresh_candidate_reference(
        candidate, "linux_nativeE2eReceipt", adapter_path
    )
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "same-actor rerun policy" in blocker


def test_rich_lifecycle_candidate_artifact_size_is_cross_bound(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    lifecycle_path = paths["linux_lifecycle"]
    lifecycle = json.loads(lifecycle_path.read_text(encoding="utf-8"))
    lifecycle["candidate"]["sizeBytes"] += 1
    write_json(lifecycle_path, lifecycle)
    refresh_desktop_lifecycle_adapter(candidate, paths, "linux")
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "rich lifecycle receipt.candidate.sizeBytes" in blocker


def test_rich_lifecycle_n_minus_one_version_is_cross_bound(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    lifecycle_path = paths["linux_lifecycle"]
    lifecycle = json.loads(lifecycle_path.read_text(encoding="utf-8"))
    changed_version = "run-20260723"
    lifecycle["nMinusOne"]["version"] = changed_version
    for kind in ("startupReceipt", "mouseFirstReceipt"):
        binding = lifecycle["coreWorkflow"]["nMinusOne"][kind]
        core_path = lifecycle_path.parent / binding["path"]
        core = json.loads(core_path.read_text(encoding="utf-8"))
        core["releaseVersion"] = changed_version
        core["version"] = changed_version
        write_json(core_path, core)
        binding["sha256"] = sha256(core_path)
        binding["sizeBytes"] = core_path.stat().st_size
        for row in lifecycle["evidenceFiles"]:
            if row["path"] == binding["path"]:
                row.update(binding)
                break

    manifest_binding = lifecycle["packageAuthority"]["manifestReceipt"]
    manifest_path = lifecycle_path.parent / manifest_binding["path"]
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["releaseVersion"] = changed_version
    manifest["version"] = changed_version
    manifest["artifacts"][0]["releaseVersion"] = changed_version
    manifest["artifacts"][0]["version"] = changed_version
    write_json(manifest_path, manifest)
    manifest_digest = sha256(manifest_path)
    lifecycle["nMinusOne"]["manifestSha256"] = manifest_digest
    lifecycle["packageAuthority"]["manifestSha256"] = manifest_digest
    manifest_binding["sha256"] = manifest_digest
    manifest_binding["sizeBytes"] = manifest_path.stat().st_size
    for row in lifecycle["evidenceFiles"]:
        if row["path"] == manifest_binding["path"]:
            row.update(manifest_binding)
            break
    write_json(lifecycle_path, lifecycle)
    refresh_desktop_lifecycle_adapter(candidate, paths, "linux")
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "live release-channel authority release version" in blocker


def test_linux_immutable_n_minus_one_manifest_bytes_are_revalidated(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    lifecycle = json.loads(
        paths["linux_lifecycle"].read_text(encoding="utf-8")
    )
    manifest_path = paths["linux_lifecycle"].parent / lifecycle[
        "packageAuthority"
    ]["manifestReceipt"]["path"]
    manifest_path.write_bytes(manifest_path.read_bytes() + b" ")
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "rich lifecycle receipt failed independent validation" in blocker
    assert "bytes differ" in blocker


def test_windows_lifecycle_signing_receipt_must_equal_candidate_authority(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    lifecycle_path = paths["windows_lifecycle"]
    lifecycle = json.loads(lifecycle_path.read_text(encoding="utf-8"))
    signing_binding = lifecycle["packageAuthority"]["candidate"][
        "signingReceipt"
    ]
    original_signing = lifecycle_path.parent / signing_binding["path"]
    copied_signing = lifecycle_path.parent / (
        "candidate-v2-signing-receipt-copy.json"
    )
    signing_payload = json.loads(original_signing.read_text(encoding="utf-8"))
    signing_payload["reason"] = "separate-but-cryptographically-valid-receipt"
    write_json(copied_signing, signing_payload)
    replacement = {
        "path": copied_signing.name,
        "role": signing_binding["role"],
        "sha256": sha256(copied_signing),
        "sizeBytes": copied_signing.stat().st_size,
    }
    lifecycle["packageAuthority"]["candidate"]["signingReceipt"] = replacement
    for index, row in enumerate(lifecycle["evidenceFiles"]):
        if row["role"] == "candidate-v2-signing-receipt":
            lifecycle["evidenceFiles"][index] = replacement
            break
    write_json(lifecycle_path, lifecycle)
    refresh_desktop_lifecycle_adapter(candidate, paths, "windows")
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "candidate v2 signing receipt SHA-256" in blocker


@pytest.mark.parametrize(
    ("field_path", "value", "message"),
    [
        (
            ("signingBackend",),
            "unapproved-backend",
            "signingBackend",
        ),
        (
            ("artifactSignatures", 0, "signerChain", "trusted"),
            False,
            "cryptographic evidence",
        ),
        (
            (
                "artifactSignatures",
                0,
                "timestamp",
                "chain",
                "trusted",
            ),
            False,
            "cryptographic evidence",
        ),
        (
            (
                "artifactSignatures",
                0,
                "verifier",
                "providerIndependent",
            ),
            False,
            "cryptographic evidence",
        ),
        (
            ("artifacts", 0, "unexpected"),
            "not-covered-by-the-contract",
            "candidate row has missing or extra fields",
        ),
    ],
)
def test_windows_keylocker_v2_evidence_fails_closed(
    tmp_path: Path,
    field_path: tuple[str | int, ...],
    value: object,
    message: str,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    signing_path = paths["windows_signing"]
    payload = json.loads(signing_path.read_text(encoding="utf-8"))
    target: object = payload
    for component in field_path[:-1]:
        target = target[component]  # type: ignore[index]
    target[field_path[-1]] = value  # type: ignore[index]
    write_json(signing_path, payload)
    refresh_candidate_reference(
        candidate, "windows_signingReceipt", signing_path
    )
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert message in blocker


def test_windows_keylocker_requires_one_exact_artifact_signature(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    signing_path = paths["windows_signing"]
    payload = json.loads(signing_path.read_text(encoding="utf-8"))
    payload["artifactSignatures"].append(
        dict(payload["artifactSignatures"][0])
    )
    write_json(signing_path, payload)
    refresh_candidate_reference(
        candidate, "windows_signingReceipt", signing_path
    )
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "one exact candidate artifactSignature" in blocker


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


def test_macos_rich_aggregate_rejects_recomputed_rejected_notary_chain(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    notary_path = paths["macos_notary_result"]
    write_json(
        notary_path,
        {
            "id": "01234567-89ab-cdef-0123-456789abcdef",
            "status": "Rejected",
        },
    )

    identity_path = paths["macos_signing_identity"]
    identity = json.loads(identity_path.read_text(encoding="utf-8"))
    identity["notarization"]["resultSha256"] = sha256(notary_path)
    write_json(identity_path, identity)

    aggregate_path = paths["macos_aggregate"]
    aggregate = json.loads(aggregate_path.read_text(encoding="utf-8"))
    for reference_key, binding_key, changed_path in (
        ("notaryResult", "notaryResultSha256", notary_path),
        (
            "signingIdentityReceipt",
            "signingIdentityReceiptSha256",
            identity_path,
        ),
    ):
        aggregate["references"][reference_key] = reference(
            candidate.parent, changed_path
        )
        aggregate["inputBindings"][binding_key] = sha256(changed_path)
    write_json(aggregate_path, aggregate)
    refresh_macos_aggregate_adapter(candidate, paths)
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "accepted notary result" in blocker


def test_macos_aggregate_signing_receipt_is_cross_bound_to_candidate(
    tmp_path: Path,
) -> None:
    candidate, paths = make_fixture(tmp_path)
    replacement = candidate.parent / "receipts" / "macos-top-signing.json"
    signing = json.loads(
        paths["macos_signing"].read_text(encoding="utf-8")
    )
    signing["operatorNote"] = "valid receipt with a different authority path"
    write_json(replacement, signing)
    refresh_candidate_reference(candidate, "macos_signingReceipt", replacement)
    output = tmp_path / "proposal.json"

    assert run_propose(candidate, output) == 1
    blocker = json.loads(output.read_text(encoding="utf-8"))["blockers"][0]
    assert "macOS aggregate signing receipt path" in blocker


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


def test_finalization_requires_distinct_workflow_run_ids(
    tmp_path: Path,
) -> None:
    candidate, _ = make_fixture(tmp_path)
    proposal = tmp_path / "proposal.json"
    assert run_propose(candidate, proposal) == 0
    approvals = make_approvals(tmp_path, proposal)
    release = json.loads(approvals[1].read_text(encoding="utf-8"))
    release["authority"]["runId"] = 201
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
    assert "distinct workflow runs" in blocker


def test_finalization_requires_one_exact_reviewer_policy(
    tmp_path: Path,
) -> None:
    candidate, _ = make_fixture(tmp_path)
    proposal = tmp_path / "proposal.json"
    assert run_propose(candidate, proposal) == 0
    approvals = make_approvals(tmp_path, proposal)
    security = json.loads(approvals[2].read_text(encoding="utf-8"))
    security["reviewerPolicy"]["sha256"] = "f" * 64
    write_json(approvals[2], security)
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
    assert "one exact reviewer policy" in blocker


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
