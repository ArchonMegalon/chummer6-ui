from __future__ import annotations

import base64
import copy
import hashlib
import importlib.util
import json
import os
import subprocess
import sys
from pathlib import Path

import pytest
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT_PATH = REPO_ROOT / "scripts" / "verify_hosted_build_v002_operator_decisions.py"
PACKET_PATH = REPO_ROOT / ".codex-design" / "product" / "HOSTED_BUILD_V002_OPERATOR_DECISIONS.json"
SOURCE_PATH = REPO_ROOT / "docs" / "HOSTED_BUILD_WORKSPACE_LIFECYCLE_AND_QUOTA_CONTRACT.md"
SPEC = importlib.util.spec_from_file_location("hosted_build_v002_operator_decisions", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)
GENERATED_AT = "2026-07-15T10:00:00Z"
TEST_ROOT_KEY_FILE = ".hosted-build-v002-approval-root-public-key.b64"


def load_packet() -> dict[str, object]:
    payload = json.loads(PACKET_PATH.read_text(encoding="utf-8"))
    assert isinstance(payload, dict)
    return payload


def packet_bytes(payload: dict[str, object]) -> bytes:
    return (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def fixture_policy_value(
    kind: str,
    decision_id: str,
    facet: str,
) -> dict[str, object]:
    if kind == "identifier":
        return {"kind": kind, "value": "fixture_choice"}
    if kind == "identifier_list":
        return {"kind": kind, "value": ["fixture_choice"]}
    if kind == "identifier_map":
        return {"kind": kind, "value": {"fixture_tier": "fixture_policy"}}
    if kind == "boolean_map":
        return {"kind": kind, "value": {"fixture_dimension": True}}
    if kind == "boolean":
        return {"kind": kind, "value": True}
    if kind == "positive_integer":
        return {"kind": kind, "value": 1}
    if kind == "bytes":
        return {"kind": kind, "value": 1024, "unit": "bytes"}
    if kind == "duration":
        days = {
            ("offline_compatibility", "operation_id_lifetime"): 2,
            ("tombstone_privacy_policy", "tombstone_retention"): 2,
            ("delete_replay_and_rpo", "ledger_retention"): 2,
        }.get((decision_id, facet), 1)
        return {"kind": kind, "value": days, "unit": "days"}
    if kind == "quota_limit_map":
        return {
            "kind": kind,
            "value": {"fixture_dimension": {"mode": "limited", "value": 1}},
        }
    if kind == "money":
        return {"kind": kind, "amountMinor": 0, "currency": "EUR"}
    raise AssertionError(f"missing fixture value for {kind}")


def evaluate(
    payload: dict[str, object],
    *,
    workspace_root: Path = REPO_ROOT.parent,
    source_bytes: bytes | None = None,
) -> dict[str, object]:
    registry_path = (
        workspace_root
        / "chummer-presentation"
        / ".codex-design"
        / "product"
        / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
    )
    if not registry_path.is_file():
        registry_path = (
            REPO_ROOT
            / ".codex-design"
            / "product"
            / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
        )
    registry_bytes = registry_path.read_bytes()
    trust_root_path = workspace_root / TEST_ROOT_KEY_FILE
    trust_root_public_key = (
        base64.b64decode(trust_root_path.read_text(encoding="ascii"), validate=True)
        if trust_root_path.is_file()
        else None
    )
    return MODULE.evaluate_packet(
        payload,
        packet_bytes=packet_bytes(payload),
        source_bytes=source_bytes if source_bytes is not None else SOURCE_PATH.read_bytes(),
        approval_registry_payload=json.loads(registry_bytes),
        approval_registry_bytes=registry_bytes,
        workspace_root=workspace_root,
        generated_at_utc=GENERATED_AT,
        approval_trust_root_public_key=trust_root_public_key,
        approval_trust_registry_sha256=(
            MODULE.digest_bytes(registry_bytes)
            if trust_root_public_key is not None
            else None
        ),
    )


def test_repository_packet_is_exactly_twelve_explicit_unresolved_decisions() -> None:
    payload = load_packet()

    receipt = evaluate(payload)

    expected_ids = [spec.decision_id for spec in MODULE.DECISION_SPECS]
    assert "status" not in payload
    assert "reviewRequired" not in payload
    assert receipt["status"] == "review_required"
    assert receipt["reviewRequired"] is True
    assert receipt["decisionGatePassed"] is False
    assert receipt["decisionCount"] == 12
    assert receipt["approvedDecisionIds"] == []
    assert receipt["unresolvedDecisionIds"] == expected_ids
    assert receipt["invalidDecisionIds"] == []
    assert receipt["validationErrors"] == []
    assert receipt["blockers"] == ["hosted_build_v002_operator_decisions_unresolved"]
    assert "hosted_build_v002_authoring" in receipt["blockedClaims"]


def test_every_operator_answer_facet_has_one_typed_value_schema() -> None:
    payload = load_packet()
    expected_facets = {
        (spec.decision_id, facet)
        for spec in MODULE.DECISION_SPECS
        for facet in spec.answer_facets
    }

    assert set(MODULE.FACET_VALUE_KINDS) == expected_facets
    assert set(MODULE.EVIDENCE_PROOF_ARTIFACT_TYPES) == set(
        MODULE.EVIDENCE_CONTRACT_NAMES
    )
    for spec, decision in zip(
        MODULE.DECISION_SPECS,
        payload["decisions"],
        strict=True,
    ):
        assert list(decision["requiredAnswerSchema"]) == list(spec.answer_facets)
        assert decision["requiredAnswerSchema"] == {
            facet: MODULE.FACET_VALUE_KINDS[(spec.decision_id, facet)]
            for facet in spec.answer_facets
        }
        assert list(decision["requiredEvidenceProofArtifacts"]) == list(
            spec.evidence_kinds
        )
        assert decision["requiredEvidenceProofArtifacts"] == {
            evidence_kind: list(
                MODULE.EVIDENCE_PROOF_ARTIFACT_TYPES[evidence_kind]
            )
            for evidence_kind in spec.evidence_kinds
        }


def test_cli_materializes_review_receipt_and_returns_one(tmp_path: Path) -> None:
    summary_path = tmp_path / "decision-gate.json"

    completed = subprocess.run(
        [
            sys.executable,
            str(SCRIPT_PATH),
            "--generated-at-utc",
            GENERATED_AT,
            "--summary-output",
            str(summary_path),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 1
    assert completed.stdout == ""
    assert completed.stderr.strip() == "hosted_build_v002_operator_decisions:review_required"
    assert json.loads(summary_path.read_text(encoding="utf-8"))["status"] == "review_required"


@pytest.mark.parametrize("mutation", ["missing", "duplicate", "reordered", "unknown"])
def test_missing_duplicate_reordered_and_unknown_decisions_fail_closed(mutation: str) -> None:
    payload = load_packet()
    decisions = payload["decisions"]
    assert isinstance(decisions, list)
    if mutation == "missing":
        decisions.pop()
    elif mutation == "duplicate":
        decisions[1] = copy.deepcopy(decisions[0])
    elif mutation == "reordered":
        decisions[0], decisions[1] = decisions[1], decisions[0]
    else:
        decisions[0]["id"] = "operator_chosen_extra"

    receipt = evaluate(payload)

    assert receipt["status"] == "invalid"
    assert receipt["reviewRequired"] is True
    assert "decision_order_or_identity_invalid" in receipt["validationErrors"]


def test_source_contract_drift_invalidates_packet_before_any_approval() -> None:
    payload = load_packet()

    receipt = evaluate(payload, source_bytes=SOURCE_PATH.read_bytes() + b"\nsource drift\n")

    assert receipt["status"] == "invalid"
    assert receipt["validationErrors"] == ["source_contract_digest_mismatch"]


def test_decision_table_change_cannot_be_accepted_by_updating_only_the_source_hash() -> None:
    payload = load_packet()
    source_bytes = SOURCE_PATH.read_bytes().replace(
        b"`quota_policy`",
        b"`quota_policy_changed`",
        1,
    )
    payload["sourceContract"]["sha256"] = MODULE.digest_bytes(source_bytes)

    receipt = evaluate(payload, source_bytes=source_bytes)

    assert receipt["status"] == "invalid"
    assert "source_decision_table_invalid" in receipt["validationErrors"]


def test_decision_table_choices_owners_and_evidence_are_source_bound() -> None:
    payload = load_packet()
    source_bytes = SOURCE_PATH.read_bytes().replace(
        b"Product, billing, operations; concurrency and UI receipts",
        b"Attacker; no evidence",
        1,
    )
    payload["sourceContract"]["sha256"] = MODULE.digest_bytes(source_bytes)

    receipt = evaluate(payload, source_bytes=source_bytes)

    assert receipt["status"] == "invalid"
    assert "source_decision_table_invalid" in receipt["validationErrors"]


def test_editable_packet_cannot_self_report_a_clear_status() -> None:
    payload = load_packet()
    payload["status"] = "pass"
    payload["reviewRequired"] = False

    receipt = evaluate(payload)

    assert receipt["status"] == "invalid"
    assert receipt["decisionGatePassed"] is False
    assert "packet_shape_invalid" in receipt["validationErrors"]


def test_unresolved_row_cannot_smuggle_a_selection_approval_or_evidence() -> None:
    payload = load_packet()
    decision = payload["decisions"][0]
    decision["resolution"]["answers"] = {
        "dimensions": {
            "disposition": "selected",
            "value": "workspace_count",
            "rationale": "Not approved; this must not be accepted while unresolved.",
        }
    }
    decision["resolution"]["approvals"] = [{"role": "product"}]
    decision["resolution"]["evidenceRefs"] = [{"kind": "concurrency_receipt"}]

    receipt = evaluate(payload)

    assert receipt["status"] == "invalid"
    assert receipt["invalidDecisionIds"] == ["quota_policy"]
    assert "quota_policy:unresolved_answers_must_be_empty" in receipt["validationErrors"]
    assert "quota_policy:unresolved_approvals_must_be_empty" in receipt["validationErrors"]
    assert "quota_policy:unresolved_evidence_must_be_empty" in receipt["validationErrors"]


def approved_packet(tmp_path: Path) -> tuple[dict[str, object], Path]:
    workspace_root = tmp_path / "chummercomplete"
    presentation_root = workspace_root / "chummer-presentation"
    evidence_root = presentation_root / ".codex-studio" / "published" / "hosted-build-v002"
    evidence_root.mkdir(parents=True)
    artifact_root = evidence_root / "artifacts"
    artifact_root.mkdir()
    payload = load_packet()
    source_bytes = SOURCE_PATH.read_bytes()
    payload["sourceContract"]["sha256"] = MODULE.digest_bytes(source_bytes)
    payload["candidateReleaseIdentity"] = "candidate-fixture"
    root_private_key = Ed25519PrivateKey.generate()
    root_public_key = root_private_key.public_key().public_bytes(
        encoding=serialization.Encoding.Raw,
        format=serialization.PublicFormat.Raw,
    )
    (workspace_root / TEST_ROOT_KEY_FILE).write_text(
        base64.b64encode(root_public_key).decode("ascii"),
        encoding="ascii",
    )
    all_roles = list(
        dict.fromkeys(
            role
            for decision_spec in MODULE.DECISION_SPECS
            for role in decision_spec.owner_roles
        )
    )
    role_private_keys = {
        role: Ed25519PrivateKey.generate()
        for role in all_roles
    }
    registry_payload = {
        "contractName": MODULE.APPROVAL_KEY_REGISTRY_CONTRACT_NAME,
        "contractVersion": 1,
        "status": "active",
        "keys": [
            {
                "keyId": f"fixture-{role}-key",
                "algorithm": "ed25519",
                "publicKeyBase64": base64.b64encode(
                    role_private_keys[role].public_key().public_bytes(
                        encoding=serialization.Encoding.Raw,
                        format=serialization.PublicFormat.Raw,
                    )
                ).decode("ascii"),
                "roles": [role],
                "actorIds": [f"{role}-operator"],
                "status": "active",
            }
            for role in all_roles
        ],
        "rootAuthorization": None,
    }
    root_authorization = {
        "authority": "external_ed25519_root",
        "rootKeyId": MODULE.approval_root_key_id(root_public_key),
        "signedAtUtc": "2026-07-14T11:00:00Z",
        "registryContentSha256": MODULE.digest_bytes(
            MODULE.canonical_bytes(
                MODULE.approval_registry_signing_material(registry_payload)
            )
        ),
    }
    root_authorization["signatureBase64"] = base64.b64encode(
        root_private_key.sign(MODULE.canonical_bytes(root_authorization))
    ).decode("ascii")
    registry_payload["rootAuthorization"] = root_authorization
    registry_bytes = (json.dumps(registry_payload, indent=2) + "\n").encode("utf-8")
    registry_path = (
        presentation_root
        / ".codex-design"
        / "product"
        / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
    )
    registry_path.parent.mkdir(parents=True, exist_ok=True)
    registry_path.write_bytes(registry_bytes)
    payload["approvalKeyRegistry"]["sha256"] = MODULE.digest_bytes(registry_bytes)

    for spec, decision in zip(MODULE.DECISION_SPECS, payload["decisions"], strict=True):
        resolution = decision["resolution"]
        owner_role = spec.owner_roles[0]
        owner_actor = f"{owner_role}-operator"
        resolution["decisionStatus"] = "approved"
        resolution["accountableOwner"] = {"role": owner_role, "actorId": owner_actor}
        resolution["answers"] = {
            facet: {
                "disposition": "selected",
                "value": fixture_policy_value(
                    MODULE.FACET_VALUE_KINDS[(spec.decision_id, facet)],
                    spec.decision_id,
                    facet,
                ),
                "rationale": f"Explicit fixture rationale for {facet}.",
            }
            for facet in spec.answer_facets
        }
        resolution["resolutionRationale"] = f"Explicit fixture resolution for {spec.decision_id}."
        resolution["approvals"] = []
        resolution["evidenceRefs"] = []
        content_digest = MODULE.decision_content_digest(
            payload["sourceContract"]["sha256"],
            spec,
            resolution,
        )
        for evidence_kind in spec.evidence_kinds:
            contract_name = MODULE.EVIDENCE_CONTRACT_NAMES[evidence_kind]
            release_identity = "candidate-fixture" if evidence_kind in MODULE.RELEASE_BOUND_EVIDENCE_KINDS else None
            proof_artifacts: list[dict[str, object]] = []
            for artifact_type in MODULE.EVIDENCE_PROOF_ARTIFACT_TYPES[evidence_kind]:
                artifact_bytes = (
                    f"fixture proof for {spec.decision_id}/{evidence_kind}/{artifact_type}\n"
                ).encode("utf-8")
                artifact_relative_path = (
                    ".codex-studio/published/hosted-build-v002/artifacts/"
                    f"{spec.decision_id}-{evidence_kind}-{artifact_type}.txt"
                )
                (presentation_root / artifact_relative_path).write_bytes(
                    artifact_bytes
                )
                proof_artifacts.append(
                    {
                        "artifactType": artifact_type,
                        "repo": "chummer-presentation",
                        "path": artifact_relative_path,
                        "sha256": MODULE.digest_bytes(artifact_bytes),
                        "byteCount": len(artifact_bytes),
                    }
                )
            evidence_payload: dict[str, object] = {
                "contractName": contract_name,
                "contractVersion": 1,
                "evidenceKind": evidence_kind,
                "status": "pass",
                "reviewRequired": False,
                "blockers": [],
                "sourceContractSha256": payload["sourceContract"]["sha256"],
                "decisionSha256": content_digest,
                "generatedAtUtc": "2026-07-14T12:00:00Z",
                "releaseIdentity": release_identity,
                "producer": {
                    "name": "fixture_evidence_producer",
                    "version": "1.0.0",
                    "runId": f"fixture-{spec.decision_id}-{evidence_kind}",
                    "invocationSha256": MODULE.digest_bytes(
                        f"fixture invocation {spec.decision_id} {evidence_kind}".encode(
                            "utf-8"
                        )
                    ),
                },
                "proofArtifacts": proof_artifacts,
            }
            evidence_bytes = (json.dumps(evidence_payload, sort_keys=True) + "\n").encode("utf-8")
            relative_path = (
                ".codex-studio/published/hosted-build-v002/"
                f"{spec.decision_id}-{evidence_kind}.json"
            )
            (presentation_root / relative_path).write_bytes(evidence_bytes)
            resolution["evidenceRefs"].append(
                {
                    "kind": evidence_kind,
                    "repo": "chummer-presentation",
                    "path": relative_path,
                    "sha256": MODULE.digest_bytes(evidence_bytes),
                    "contractName": contract_name,
                    "releaseIdentity": release_identity,
                }
            )
        digest = MODULE.decision_digest(
            payload["sourceContract"]["sha256"],
            spec,
            resolution,
        )
        approvals: list[dict[str, object]] = []
        for role in spec.owner_roles:
            actor_id = owner_actor if role == owner_role else f"{role}-operator"
            key_id = f"fixture-{role}-key"
            approved_at = "2026-07-14T12:00:00Z"
            attestation_payload = {
                "contractName": MODULE.APPROVAL_CONTRACT_NAME,
                "contractVersion": 1,
                "authority": "ed25519_role_registry",
                "status": "approved",
                "reviewRequired": False,
                "blockers": [],
                "role": role,
                "actorId": actor_id,
                "approvedAtUtc": approved_at,
                "sourceContractSha256": payload["sourceContract"]["sha256"],
                "decisionSha256": digest,
                "keyId": key_id,
            }
            attestation_payload["signatureBase64"] = base64.b64encode(
                role_private_keys[role].sign(MODULE.canonical_bytes(attestation_payload))
            ).decode("ascii")
            attestation_bytes = (
                json.dumps(attestation_payload, sort_keys=True) + "\n"
            ).encode("utf-8")
            attestation_path = (
                ".codex-studio/published/hosted-build-v002/approvals/"
                f"{spec.decision_id}-{role}.json"
            )
            absolute_attestation_path = presentation_root / attestation_path
            absolute_attestation_path.parent.mkdir(parents=True, exist_ok=True)
            absolute_attestation_path.write_bytes(attestation_bytes)
            approvals.append(
                {
                "role": role,
                "actorId": actor_id,
                "approvedAtUtc": approved_at,
                "decisionSha256": digest,
                    "keyId": key_id,
                "attestationRef": {
                    "repo": "chummer-presentation",
                    "path": attestation_path,
                    "sha256": MODULE.digest_bytes(attestation_bytes),
                    "contractName": MODULE.APPROVAL_CONTRACT_NAME,
                },
            }
            )
        resolution["approvals"] = approvals
    return payload, workspace_root


def test_fully_explicit_digest_bound_fixture_passes_decision_freeze_only(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert receipt["status"] == "pass"
    assert receipt["reviewRequired"] is False
    assert receipt["decisionGatePassed"] is True
    assert receipt["approvedDecisionIds"] == [spec.decision_id for spec in MODULE.DECISION_SPECS]
    assert receipt["unresolvedDecisionIds"] == []
    assert receipt["blockedClaims"] == []
    assert "migration, production, privacy, recovery" in receipt["reason"]


def test_partial_approval_stays_review_required_without_becoming_invalid(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    resolution = payload["decisions"][1]["resolution"]
    resolution.update(
        {
            "decisionStatus": "unresolved",
            "accountableOwner": None,
            "answers": {},
            "resolutionRationale": None,
            "approvals": [],
            "evidenceRefs": [],
        }
    )

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert receipt["status"] == "review_required"
    assert receipt["validationErrors"] == []
    assert receipt["approvedDecisionIds"] == [
        spec.decision_id for spec in MODULE.DECISION_SPECS if spec.decision_id != "logical_bytes"
    ]
    assert receipt["unresolvedDecisionIds"] == ["logical_bytes"]


def test_answer_object_key_order_is_not_an_approval_semantic(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    spec = MODULE.DECISION_SPECS[0]
    resolution = payload["decisions"][0]["resolution"]
    resolution["answers"] = dict(reversed(list(resolution["answers"].items())))
    digest = MODULE.decision_digest(payload["sourceContract"]["sha256"], spec, resolution)
    for approval in resolution["approvals"]:
        approval["decisionSha256"] = digest

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert receipt["status"] == "pass"
    assert receipt["validationErrors"] == []


def test_approval_digest_and_future_timestamp_are_rejected(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    approval = payload["decisions"][0]["resolution"]["approvals"][0]
    approval["decisionSha256"] = "sha256:" + ("0" * 64)
    approval["approvedAtUtc"] = "2026-07-16T12:00:00Z"

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert receipt["status"] == "invalid"
    assert "quota_policy:approval_digest_mismatch" in receipt["validationErrors"]
    assert "quota_policy:approval_time_invalid" in receipt["validationErrors"]


def test_approval_attestation_is_required_and_digest_bound(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    approval = payload["decisions"][0]["resolution"]["approvals"][0]
    approval["attestationRef"]["sha256"] = "sha256:" + ("e" * 64)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert (
        "quota_policy:approval:product:attestation_digest_mismatch"
        in receipt["validationErrors"]
    )


def test_approval_signature_must_match_an_active_role_key(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    approval = payload["decisions"][0]["resolution"]["approvals"][0]
    attestation_path = (
        workspace_root
        / "chummer-presentation"
        / approval["attestationRef"]["path"]
    )
    attestation = json.loads(attestation_path.read_text(encoding="utf-8"))
    attestation["signatureBase64"] = base64.b64encode(b"\0" * 64).decode("ascii")
    attestation_bytes = (json.dumps(attestation, sort_keys=True) + "\n").encode("utf-8")
    attestation_path.write_bytes(attestation_bytes)
    approval["attestationRef"]["sha256"] = MODULE.digest_bytes(attestation_bytes)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "quota_policy:approval:product:attestation_signature_invalid" in receipt[
        "validationErrors"
    ]


def test_approved_packet_cannot_use_unconfigured_operator_key_registry(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    registry_path = (
        workspace_root
        / "chummer-presentation"
        / ".codex-design"
        / "product"
        / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
    )
    registry_bytes = (
        REPO_ROOT
        / ".codex-design"
        / "product"
        / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
    ).read_bytes()
    registry_path.write_bytes(registry_bytes)
    payload["approvalKeyRegistry"]["sha256"] = MODULE.digest_bytes(registry_bytes)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "quota_policy:approval_key_registry_not_active" in receipt["validationErrors"]
    assert "quota_policy:approval:product:attestation_key_untrusted" in receipt[
        "validationErrors"
    ]


def test_active_registry_requires_an_external_root_not_just_repo_edits(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    registry_path = (
        workspace_root
        / "chummer-presentation"
        / ".codex-design"
        / "product"
        / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
    )
    registry_bytes = registry_path.read_bytes()
    monkeypatch.delenv(MODULE.APPROVAL_ROOT_PUBLIC_KEY_ENV, raising=False)

    receipt = MODULE.evaluate_packet(
        payload,
        packet_bytes=packet_bytes(payload),
        source_bytes=SOURCE_PATH.read_bytes(),
        approval_registry_payload=json.loads(registry_bytes),
        approval_registry_bytes=registry_bytes,
        workspace_root=workspace_root,
        generated_at_utc=GENERATED_AT,
    )

    assert receipt["status"] == "invalid"
    assert "approval_key_registry_root_key_unavailable" in receipt["validationErrors"]
    assert receipt["approvalKeyRegistry"]["activeKeyCount"] == 0


def test_active_registry_must_match_the_independently_pinned_current_digest(
    tmp_path: Path,
) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    registry_path = (
        workspace_root
        / "chummer-presentation"
        / ".codex-design"
        / "product"
        / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
    )
    registry_bytes = registry_path.read_bytes()
    trust_root_public_key = base64.b64decode(
        (workspace_root / TEST_ROOT_KEY_FILE).read_text(encoding="ascii"),
        validate=True,
    )

    receipt = MODULE.evaluate_packet(
        payload,
        packet_bytes=packet_bytes(payload),
        source_bytes=SOURCE_PATH.read_bytes(),
        approval_registry_payload=json.loads(registry_bytes),
        approval_registry_bytes=registry_bytes,
        workspace_root=workspace_root,
        generated_at_utc=GENERATED_AT,
        approval_trust_root_public_key=trust_root_public_key,
        approval_trust_registry_sha256="sha256:" + ("0" * 64),
    )

    assert receipt["status"] == "invalid"
    assert "approval_key_registry_external_digest_mismatch" in receipt[
        "validationErrors"
    ]
    assert receipt["approvalKeyRegistry"]["activeKeyCount"] == 0


def test_stale_registry_root_authorization_cannot_remain_active(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    registry_path = (
        workspace_root
        / "chummer-presentation"
        / ".codex-design"
        / "product"
        / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
    )
    registry = json.loads(registry_path.read_text(encoding="utf-8"))
    registry["rootAuthorization"]["signedAtUtc"] = "2000-01-01T00:00:00Z"
    registry_bytes = (json.dumps(registry, indent=2) + "\n").encode("utf-8")
    registry_path.write_bytes(registry_bytes)
    payload["approvalKeyRegistry"]["sha256"] = MODULE.digest_bytes(registry_bytes)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "approval_key_registry_root_authorization_stale" in receipt[
        "validationErrors"
    ]
    assert receipt["approvalKeyRegistry"]["activeKeyCount"] == 0


def test_role_key_cannot_assert_an_unregistered_actor_identity(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    spec = MODULE.DECISION_SPECS[0]
    resolution = payload["decisions"][0]["resolution"]
    approval = resolution["approvals"][0]
    registry_path = (
        workspace_root
        / "chummer-presentation"
        / ".codex-design"
        / "product"
        / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
    )
    registry = json.loads(registry_path.read_text(encoding="utf-8"))
    registered_key = registry["keys"][0]
    trusted_keys = {
        registered_key["keyId"]: {
            "public_key": base64.b64decode(registered_key["publicKeyBase64"]),
            "roles": tuple(registered_key["roles"]),
            "actor_ids": ("different-registered-actor",),
        }
    }

    errors = MODULE._validate_approval_attestation(
        spec.decision_id,
        approval,
        workspace_root=workspace_root,
        source_sha256=payload["sourceContract"]["sha256"],
        decision_sha256=MODULE.decision_digest(
            payload["sourceContract"]["sha256"],
            spec,
            resolution,
        ),
        observed_at=MODULE._parse_utc(GENERATED_AT),
        trusted_approval_keys=trusted_keys,
    )

    assert errors == [
        "quota_policy:approval:product:attestation_key_actor_unauthorized"
    ]


def test_multi_role_decision_requires_distinct_actors_and_keys(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    approvals = payload["decisions"][0]["resolution"]["approvals"]
    approvals[1]["actorId"] = approvals[0]["actorId"]
    approvals[1]["keyId"] = approvals[0]["keyId"]

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "quota_policy:approval_actors_must_be_distinct" in receipt[
        "validationErrors"
    ]
    assert "quota_policy:approval_keys_must_be_distinct" in receipt[
        "validationErrors"
    ]


def test_revoked_and_active_registry_entries_cannot_reuse_a_key_id(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    registry_path = (
        workspace_root
        / "chummer-presentation"
        / ".codex-design"
        / "product"
        / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
    )
    registry = json.loads(registry_path.read_text(encoding="utf-8"))
    revoked = copy.deepcopy(registry["keys"][0])
    revoked["status"] = "revoked"
    registry["keys"].insert(0, revoked)
    registry_bytes = (json.dumps(registry, indent=2) + "\n").encode("utf-8")
    registry_path.write_bytes(registry_bytes)
    payload["approvalKeyRegistry"]["sha256"] = MODULE.digest_bytes(registry_bytes)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "approval_key_id_invalid_or_duplicate" in receipt["validationErrors"]


def test_distinct_key_ids_cannot_reuse_the_same_ed25519_public_key(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    registry_path = (
        workspace_root
        / "chummer-presentation"
        / ".codex-design"
        / "product"
        / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
    )
    registry = json.loads(registry_path.read_text(encoding="utf-8"))
    registry["keys"][1]["publicKeyBase64"] = registry["keys"][0][
        "publicKeyBase64"
    ]
    registry_bytes = (json.dumps(registry, indent=2) + "\n").encode("utf-8")
    registry_path.write_bytes(registry_bytes)
    payload["approvalKeyRegistry"]["sha256"] = MODULE.digest_bytes(registry_bytes)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "approval_key:fixture-billing-key:public_key_duplicate" in receipt[
        "validationErrors"
    ]


def test_malformed_registry_role_types_fail_closed_instead_of_crashing(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    registry_path = (
        workspace_root
        / "chummer-presentation"
        / ".codex-design"
        / "product"
        / "HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
    )
    registry = json.loads(registry_path.read_text(encoding="utf-8"))
    registry["keys"][0]["roles"] = [{}]
    registry_bytes = (json.dumps(registry, indent=2) + "\n").encode("utf-8")
    registry_path.write_bytes(registry_bytes)
    payload["approvalKeyRegistry"]["sha256"] = MODULE.digest_bytes(registry_bytes)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert receipt["status"] == "invalid"
    assert "approval_key:fixture-product-key:roles_invalid" in receipt[
        "validationErrors"
    ]


def test_placeholder_and_nested_null_policy_values_are_not_decisions(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    answers = payload["decisions"][0]["resolution"]["answers"]
    answers["dimensions"]["value"] = "TBD"
    answers["numeric_limits"]["value"] = {"workspace_count": None}

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "quota_policy:answer:dimensions:selected_value_required" in receipt["validationErrors"]
    assert "quota_policy:answer:numeric_limits:selected_value_required" in receipt["validationErrors"]


def test_policy_facets_require_their_declared_type_and_unit(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    payload["decisions"][0]["resolution"]["answers"]["numeric_limits"]["value"] = {
        "kind": "identifier",
        "value": "not_a_limit_map",
    }
    payload["decisions"][3]["resolution"]["answers"]["maximum_client_age"]["value"] = {
        "kind": "duration",
        "value": 1,
        "unit": "weeks",
    }

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "quota_policy:answer:numeric_limits:selected_value_required" in receipt[
        "validationErrors"
    ]
    assert "offline_compatibility:answer:maximum_client_age:selected_value_required" in receipt[
        "validationErrors"
    ]


def test_policy_numbers_are_bounded_to_the_signed_64_bit_envelope(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    payload["decisions"][3]["resolution"]["answers"]["maximum_client_age"][
        "value"
    ]["value"] = 2**63

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "offline_compatibility:answer:maximum_client_age:selected_value_required" in receipt[
        "validationErrors"
    ]


def test_not_applicable_is_allowed_only_for_explicitly_conditional_facets(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    answer = payload["decisions"][0]["resolution"]["answers"]["dimensions"]
    answer.update(
        {
            "disposition": "not_applicable",
            "value": None,
            "rationale": "This attempted waiver must fail because dimensions are mandatory.",
        }
    )

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "quota_policy:answer:dimensions:not_applicable_forbidden" in receipt["validationErrors"]


def test_rejected_compressed_input_requires_both_limits_to_be_inapplicable(
    tmp_path: Path,
) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    answers = payload["decisions"][1]["resolution"]["answers"]
    answers["compressed_input_accepted"]["value"]["value"] = False

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert (
        "logical_bytes:cross_policy:reject_requires_compression_limits_not_applicable"
        in receipt["validationErrors"]
    )


def test_accepted_compressed_input_requires_both_declared_limits(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    answer = payload["decisions"][1]["resolution"]["answers"][
        "compressed_pre_decompression_limit"
    ]
    answer.update(
        {
            "disposition": "not_applicable",
            "value": None,
            "rationale": "Fixture attempts to omit a required accepted-input limit.",
        }
    )

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert (
        "logical_bytes:cross_policy:accepted_compression_requires_both_limits"
        in receipt["validationErrors"]
    )


def test_physical_lineage_erasure_requires_an_explicit_reuse_fence(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    answers = payload["decisions"][4]["resolution"]["answers"]
    answers["physical_lineage_erasure_required"]["value"]["value"] = True
    answers["independent_lineage_reuse_fence"].update(
        {
            "disposition": "not_applicable",
            "value": None,
            "rationale": "Fixture attempts to omit the physical-erasure reuse fence.",
        }
    )

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert (
        "tombstone_privacy_policy:cross_policy:physical_erasure_requires_reuse_fence"
        in receipt["validationErrors"]
    )


def test_operation_id_lifetime_cannot_expire_before_supported_clients(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    answers = payload["decisions"][3]["resolution"]["answers"]
    answers["maximum_client_age"]["value"]["value"] = 3
    answers["operation_id_lifetime"]["value"]["value"] = 2

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert (
        "offline_compatibility:cross_policy:operation_id_lifetime_shorter_than_maximum_client_age"
        in receipt["validationErrors"]
    )


@pytest.mark.parametrize(
    ("decision_index", "facet", "expected_error"),
    [
        (
            7,
            "ledger_retention",
            "delete_replay_and_rpo:cross_policy:ledger_retention_below_recoverable_window_plus_safety_margin",
        ),
        (
            4,
            "tombstone_retention",
            "tombstone_privacy_policy:cross_policy:tombstone_retention_below_recoverable_window_plus_safety_margin",
        ),
    ],
)
def test_delete_truth_retention_covers_backup_plus_explicit_safety_margin(
    tmp_path: Path,
    decision_index: int,
    facet: str,
    expected_error: str,
) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    payload["decisions"][decision_index]["resolution"]["answers"][facet]["value"][
        "value"
    ] = 1

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert expected_error in receipt["validationErrors"]


def test_delete_truth_retention_uses_the_longer_wal_pitr_window(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    capacity_answers = payload["decisions"][11]["resolution"]["answers"]
    capacity_answers["backup_retention"]["value"]["value"] = 1
    capacity_answers["wal_retention"]["value"]["value"] = 100

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert (
        "delete_replay_and_rpo:cross_policy:ledger_retention_below_recoverable_window_plus_safety_margin"
        in receipt["validationErrors"]
    )
    assert (
        "tombstone_privacy_policy:cross_policy:tombstone_retention_below_recoverable_window_plus_safety_margin"
        in receipt["validationErrors"]
    )


def test_evidence_digest_mismatch_and_symlink_are_rejected(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    evidence = payload["decisions"][0]["resolution"]["evidenceRefs"][0]
    evidence["sha256"] = "sha256:" + ("f" * 64)
    first = evaluate(payload, workspace_root=workspace_root)
    assert "quota_policy:evidence:concurrency_receipt:digest_mismatch" in first["validationErrors"]
    assert "quota_policy:approval_digest_mismatch" in first["validationErrors"]

    payload, workspace_root = approved_packet(tmp_path / "second")
    evidence = payload["decisions"][0]["resolution"]["evidenceRefs"][0]
    target = workspace_root / "chummer-presentation" / evidence["path"]
    real_target = target.with_suffix(".real.json")
    target.rename(real_target)
    target.symlink_to(real_target.name)
    second = evaluate(payload, workspace_root=workspace_root)
    assert any(
        error.startswith("quota_policy:evidence:concurrency_receipt:file_unavailable_or_unsafe")
        for error in second["validationErrors"]
    )


def test_non_array_evidence_blockers_are_not_treated_as_clear(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    evidence = payload["decisions"][0]["resolution"]["evidenceRefs"][0]
    evidence_path = workspace_root / "chummer-presentation" / evidence["path"]
    evidence_payload = json.loads(evidence_path.read_text(encoding="utf-8"))
    evidence_payload["blockers"] = "none"
    evidence_bytes = (json.dumps(evidence_payload, sort_keys=True) + "\n").encode("utf-8")
    evidence_path.write_bytes(evidence_bytes)
    evidence["sha256"] = MODULE.digest_bytes(evidence_bytes)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "quota_policy:evidence:concurrency_receipt:receipt_not_clear" in receipt["validationErrors"]


def test_evidence_status_wrapper_without_required_proof_artifacts_is_rejected(
    tmp_path: Path,
) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    evidence = payload["decisions"][0]["resolution"]["evidenceRefs"][0]
    evidence_path = workspace_root / "chummer-presentation" / evidence["path"]
    evidence_payload = json.loads(evidence_path.read_text(encoding="utf-8"))
    evidence_payload.pop("proofArtifacts")
    evidence_bytes = (json.dumps(evidence_payload, sort_keys=True) + "\n").encode(
        "utf-8"
    )
    evidence_path.write_bytes(evidence_bytes)
    evidence["sha256"] = MODULE.digest_bytes(evidence_bytes)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "quota_policy:evidence:concurrency_receipt:receipt_not_clear" in receipt[
        "validationErrors"
    ]


def test_evidence_proof_artifact_bytes_are_digest_and_size_bound(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    evidence = payload["decisions"][0]["resolution"]["evidenceRefs"][0]
    evidence_path = workspace_root / "chummer-presentation" / evidence["path"]
    evidence_payload = json.loads(evidence_path.read_text(encoding="utf-8"))
    proof_artifact = evidence_payload["proofArtifacts"][0]
    proof_artifact["sha256"] = "sha256:" + ("0" * 64)
    evidence_bytes = (json.dumps(evidence_payload, sort_keys=True) + "\n").encode(
        "utf-8"
    )
    evidence_path.write_bytes(evidence_bytes)
    evidence["sha256"] = MODULE.digest_bytes(evidence_bytes)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert (
        "quota_policy:evidence:concurrency_receipt:proof:concurrency_test_report:digest_mismatch"
        in receipt["validationErrors"]
    )


def test_one_proof_file_cannot_satisfy_multiple_artifact_types(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    evidence = payload["decisions"][0]["resolution"]["evidenceRefs"][0]
    evidence_path = workspace_root / "chummer-presentation" / evidence["path"]
    evidence_payload = json.loads(evidence_path.read_text(encoding="utf-8"))
    first = evidence_payload["proofArtifacts"][0]
    second = evidence_payload["proofArtifacts"][1]
    for field in ("repo", "path", "sha256", "byteCount"):
        second[field] = first[field]
    evidence_bytes = (json.dumps(evidence_payload, sort_keys=True) + "\n").encode(
        "utf-8"
    )
    evidence_path.write_bytes(evidence_bytes)
    evidence["sha256"] = MODULE.digest_bytes(evidence_bytes)

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "quota_policy:evidence:concurrency_receipt:receipt_not_clear" in receipt[
        "validationErrors"
    ]


def test_stale_evidence_and_approval_timestamps_fail_closed(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    decision = payload["decisions"][0]
    evidence = decision["resolution"]["evidenceRefs"][0]
    evidence_path = workspace_root / "chummer-presentation" / evidence["path"]
    evidence_payload = json.loads(evidence_path.read_text(encoding="utf-8"))
    evidence_payload["generatedAtUtc"] = "2000-01-01T00:00:00Z"
    evidence_bytes = (json.dumps(evidence_payload, sort_keys=True) + "\n").encode(
        "utf-8"
    )
    evidence_path.write_bytes(evidence_bytes)
    evidence["sha256"] = MODULE.digest_bytes(evidence_bytes)
    decision["resolution"]["approvals"][0][
        "approvedAtUtc"
    ] = "2000-01-01T00:00:00Z"

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "quota_policy:evidence:concurrency_receipt:receipt_not_clear" in receipt[
        "validationErrors"
    ]
    assert "quota_policy:approval_time_invalid" in receipt["validationErrors"]


def test_generic_self_named_evidence_contract_cannot_satisfy_a_kind(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    evidence = payload["decisions"][0]["resolution"]["evidenceRefs"][0]
    evidence["contractName"] = "test.concurrency_receipt"

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert "quota_policy:evidence:concurrency_receipt:contract_name_invalid" in receipt[
        "validationErrors"
    ]


def test_exact_image_evidence_is_bound_to_packet_candidate_release(tmp_path: Path) -> None:
    payload, workspace_root = approved_packet(tmp_path)
    payload["candidateReleaseIdentity"] = "different-candidate"

    receipt = evaluate(payload, workspace_root=workspace_root)

    assert (
        "migration_posture:evidence:exact_image_rehearsal:release_identity_required"
        in receipt["validationErrors"]
    )


def test_decision_digest_is_key_order_and_unicode_normalization_stable() -> None:
    payload = load_packet()
    spec = MODULE.DECISION_SPECS[0]
    resolution = payload["decisions"][0]["resolution"]
    resolution["accountableOwner"] = {"role": "product", "actorId": "product-operator"}
    resolution["answers"] = {
        "b": {"value": "Cafe\u0301"},
        "a": {"value": "Café"},
    }
    resolution["resolutionRationale"] = "Stable digest."
    reverse = copy.deepcopy(resolution)
    reverse["answers"] = dict(reversed(list(reverse["answers"].items())))

    first = MODULE.decision_digest(payload["sourceContract"]["sha256"], spec, resolution)
    second = MODULE.decision_digest(payload["sourceContract"]["sha256"], spec, reverse)

    assert first == second


def test_malformed_packet_returns_two_and_writes_only_fail_closed_summary(tmp_path: Path) -> None:
    packet_path = tmp_path / "packet.json"
    summary_path = tmp_path / "summary.json"
    packet_path.write_text("{not-json", encoding="utf-8")

    completed = subprocess.run(
        [
            sys.executable,
            str(SCRIPT_PATH),
            "--packet",
            str(packet_path),
            "--generated-at-utc",
            GENERATED_AT,
            "--summary-output",
            str(summary_path),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 2
    receipt = json.loads(summary_path.read_text(encoding="utf-8"))
    assert receipt["status"] == "invalid"
    assert receipt["decisionGatePassed"] is False
    assert receipt["validationErrors"] == ["packet_json_invalid"]


def test_unhashable_decision_id_fails_closed_instead_of_crashing() -> None:
    payload = load_packet()
    payload["decisions"][0]["id"] = {}

    receipt = evaluate(payload)

    assert receipt["status"] == "invalid"
    assert "decision_ids_must_be_strings" in receipt["validationErrors"]


def test_future_cli_trust_clock_writes_an_invalid_receipt(tmp_path: Path) -> None:
    summary_path = tmp_path / "future-clock.json"

    completed = subprocess.run(
        [
            sys.executable,
            str(SCRIPT_PATH),
            "--generated-at-utc",
            "2099-01-01T00:00:00Z",
            "--summary-output",
            str(summary_path),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 2
    receipt = json.loads(summary_path.read_text(encoding="utf-8"))
    assert receipt["status"] == "invalid"
    assert receipt["validationErrors"] == ["generated_at_utc_future"]


def test_stale_cli_trust_clock_writes_an_invalid_receipt(tmp_path: Path) -> None:
    summary_path = tmp_path / "stale-clock.json"

    completed = subprocess.run(
        [
            sys.executable,
            str(SCRIPT_PATH),
            "--generated-at-utc",
            "2000-01-01T00:00:00Z",
            "--summary-output",
            str(summary_path),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 2
    receipt = json.loads(summary_path.read_text(encoding="utf-8"))
    assert receipt["status"] == "invalid"
    assert receipt["validationErrors"] == ["generated_at_utc_stale"]


@pytest.mark.parametrize(
    "packet_text",
    [
        '{"contractName":"first","contractName":"second"}',
        '{"contractName":NaN}',
    ],
)
def test_duplicate_keys_and_non_finite_json_are_malformed(
    tmp_path: Path,
    packet_text: str,
) -> None:
    packet_path = tmp_path / "packet.json"
    summary_path = tmp_path / "summary.json"
    packet_path.write_text(packet_text, encoding="utf-8")

    completed = subprocess.run(
        [
            sys.executable,
            str(SCRIPT_PATH),
            "--packet",
            str(packet_path),
            "--generated-at-utc",
            GENERATED_AT,
            "--summary-output",
            str(summary_path),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 2
    assert json.loads(summary_path.read_text(encoding="utf-8"))["validationErrors"] == [
        "packet_json_invalid"
    ]


def test_packet_ancestor_symlink_is_rejected(tmp_path: Path) -> None:
    real_directory = tmp_path / "real"
    real_directory.mkdir()
    packet_path = real_directory / "packet.json"
    packet_path.write_bytes(PACKET_PATH.read_bytes())
    linked_directory = tmp_path / "linked"
    linked_directory.symlink_to(real_directory, target_is_directory=True)

    with pytest.raises(ValueError, match="packet_unavailable_or_unsafe"):
        MODULE._load_packet(linked_directory / "packet.json")


def test_noncanonical_cli_input_cannot_claim_canonical_provenance(tmp_path: Path) -> None:
    packet_path = tmp_path / "packet.json"
    summary_path = tmp_path / "summary.json"
    packet_path.write_bytes(PACKET_PATH.read_bytes())

    completed = subprocess.run(
        [
            sys.executable,
            str(SCRIPT_PATH),
            "--packet",
            str(packet_path),
            "--generated-at-utc",
            GENERATED_AT,
            "--summary-output",
            str(summary_path),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 2
    receipt = json.loads(summary_path.read_text(encoding="utf-8"))
    assert receipt["canonicalProvenance"] is False
    assert receipt["packet"]["path"] == "noncanonical-input"
    assert "canonical_provenance_required" in receipt["validationErrors"]


def test_summary_output_symlink_is_rejected_without_touching_target(tmp_path: Path) -> None:
    target = tmp_path / "target.json"
    target.write_text("operator-owned\n", encoding="utf-8")
    summary_path = tmp_path / "summary.json"
    summary_path.symlink_to(target.name)

    completed = subprocess.run(
        [
            sys.executable,
            str(SCRIPT_PATH),
            "--generated-at-utc",
            GENERATED_AT,
            "--summary-output",
            str(summary_path),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 2
    assert completed.stderr.strip() == "hosted_build_v002_operator_decisions:summary_write_failed"
    assert target.read_text(encoding="utf-8") == "operator-owned\n"


def test_verifier_does_not_author_or_reference_a_v002_migration() -> None:
    source = SCRIPT_PATH.read_text(encoding="utf-8")

    assert "V002__" not in source
    assert "CREATE TABLE" not in source
    assert "ALTER TABLE" not in source
    assert "apply migration" not in source.casefold()
