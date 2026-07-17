#!/usr/bin/env python3
"""Validate the Hosted Build V002 operator decisions without choosing them.

Exit 0 means the decision-freeze prerequisite passed. Exit 1 means the packet is
valid but still review-required. Exit 2 means the packet, source binding, or an
approval/evidence binding is invalid. A pass never authorizes a migration or a
production launch; those remain separate gates.
"""

from __future__ import annotations

import argparse
import base64
import binascii
import hashlib
import json
import os
import re
import secrets
import stat
import sys
import unicodedata
from dataclasses import dataclass
from datetime import UTC, datetime, timedelta
from pathlib import Path, PurePosixPath
from typing import Any, Iterable

try:
    from cryptography.exceptions import InvalidSignature
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PublicKey
except ImportError:  # pragma: no cover - production fails closed through registry validation.
    InvalidSignature = ValueError  # type: ignore[assignment]
    Ed25519PublicKey = None  # type: ignore[assignment,misc]


REPO_ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = REPO_ROOT.parent
DEFAULT_PACKET = REPO_ROOT / ".codex-design" / "product" / "HOSTED_BUILD_V002_OPERATOR_DECISIONS.json"
DEFAULT_SUMMARY = REPO_ROOT / ".codex-studio" / "published" / "HOSTED_BUILD_V002_OPERATOR_DECISION_GATE.generated.json"
SOURCE_CONTRACT_PATH = "docs/HOSTED_BUILD_WORKSPACE_LIFECYCLE_AND_QUOTA_CONTRACT.md"
APPROVAL_KEY_REGISTRY_PATH = (
    ".codex-design/product/HOSTED_BUILD_V002_APPROVAL_KEY_REGISTRY.json"
)
APPROVAL_KEY_REGISTRY_CONTRACT_NAME = (
    "chummer.hosted_build_v002.approval_key_registry"
)
PACKET_CONTRACT_NAME = "chummer.hosted_build_v002_operator_decisions"
PACKET_CONTRACT_VERSION = 1
PACKET_SCOPE = "hosted_build_workspace_lifecycle_and_quota_v002"
RECEIPT_CONTRACT_NAME = "chummer.hosted_build_v002_operator_decision_gate"
RECEIPT_CONTRACT_VERSION = 1
SHA256_PATTERN = re.compile(r"^sha256:[0-9a-f]{64}$")
UTC_PATTERN = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")
ACTOR_PATTERN = re.compile(r"^[a-z0-9][a-z0-9._:-]{2,127}$")
MAX_PACKET_BYTES = 2 * 1024 * 1024
MAX_EVIDENCE_BYTES = 8 * 1024 * 1024
MAX_POLICY_INTEGER = (2**63) - 1
MAX_RECEIPT_AGE = timedelta(hours=24)
MAX_ROOT_AUTHORIZATION_AGE = timedelta(days=30)
MAX_EVIDENCE_AGE = timedelta(days=30)
MAX_APPROVAL_AGE = timedelta(days=30)
APPROVAL_ROOT_PUBLIC_KEY_ENV = (
    "CHUMMER_HOSTED_BUILD_V002_APPROVAL_ROOT_PUBLIC_KEY_BASE64"
)
APPROVAL_REGISTRY_SHA256_ENV = (
    "CHUMMER_HOSTED_BUILD_V002_APPROVAL_REGISTRY_SHA256"
)
BLOCKED_CLAIMS = [
    "flagship_launch",
    "public_release_supportability",
    "hosted_build_v002_contract_freeze",
    "hosted_build_v002_authoring",
    "hosted_build_v002_migration",
    "hosted_build_production_launch",
]
DOES_NOT_AUTHORIZE = [
    "hosted_build_v002_authoring",
    "hosted_build_v002_application",
    "quota_enforcement",
    "tombstone_deletion",
    "hosted_build_production_launch",
    "public_recovery_or_retention_claims",
]
RELEASE_BOUND_EVIDENCE_KINDS = {"exact_image_rehearsal"}
EVIDENCE_PATH_PREFIX = ".codex-studio/published/hosted-build-v002/"
EVIDENCE_PROOF_ARTIFACT_PATH_PREFIX = (
    ".codex-studio/published/hosted-build-v002/artifacts/"
)
APPROVAL_PATH_PREFIX = ".codex-studio/published/hosted-build-v002/approvals/"
APPROVAL_CONTRACT_NAME = "chummer.hosted_build_v002.operator_approval.v1"
PLACEHOLDER_TOKENS = {
    "n/a",
    "na",
    "none",
    "not decided",
    "pending",
    "tbd",
    "todo",
    "unresolved",
}


@dataclass(frozen=True)
class DecisionSpec:
    decision_id: str
    title: str
    owner_roles: tuple[str, ...]
    answer_facets: tuple[str, ...]
    evidence_kinds: tuple[str, ...]


DECISION_SPECS = (
    DecisionSpec(
        "quota_policy",
        "Quota policy",
        ("product", "billing", "operations"),
        (
            "dimensions",
            "numeric_limits",
            "tier_mapping",
            "limit_change_behavior",
            "tombstone_receipt_user_visible_accounting",
        ),
        ("concurrency_receipt", "ui_receipt"),
    ),
    DecisionSpec(
        "logical_bytes",
        "Logical bytes",
        ("product", "security", "engineering"),
        (
            "algorithm",
            "algorithm_version",
            "canonicalization_change_policy",
            "compressed_input_treatment",
            "compressed_input_accepted",
            "compressed_pre_decompression_limit",
            "compressed_post_decompression_limit",
        ),
        ("cross_provider_byte_vectors",),
    ),
    DecisionSpec(
        "recreation_and_undo",
        "Recreation and undo",
        ("product",),
        ("id_recreation_policy", "authorized_requesters", "visible_recovery_promise"),
        ("lifecycle_tests", "roaming_tests"),
    ),
    DecisionSpec(
        "offline_compatibility",
        "Offline compatibility",
        ("product", "support"),
        (
            "maximum_client_age",
            "operation_id_lifetime",
            "older_client_write_disable_behavior",
            "expired_operation_id_reuse_fence",
        ),
        ("client_telemetry", "replay_tests"),
    ),
    DecisionSpec(
        "tombstone_privacy_policy",
        "Tombstone/privacy policy",
        ("legal", "privacy", "product"),
        (
            "tombstone_retention",
            "purge_policy",
            "legal_hold_policy",
            "erasure_policy",
            "physical_lineage_erasure_required",
            "owner_visible_gone_behavior",
            "independent_lineage_reuse_fence",
        ),
        ("approved_retention_matrix",),
    ),
    DecisionSpec(
        "stable_owner_identity",
        "Stable owner identity",
        ("security", "identity"),
        ("identity_model", "key_alias_versioning", "hmac_kms_posture", "rotation_recovery"),
        ("rotation_review", "enumeration_review"),
    ),
    DecisionSpec(
        "writer_epoch",
        "Writer epoch",
        ("operations", "security"),
        ("external_authority", "allocation", "rotation", "fencing", "outage_behavior"),
        ("split_writer_negative_proof",),
    ),
    DecisionSpec(
        "delete_replay_and_rpo",
        "Delete replay and RPO",
        ("legal", "product", "operations"),
        (
            "independent_event_durability",
            "replay_checkpoint",
            "acknowledged_delete_loss_tolerance",
            "ledger_retention",
            "safety_margin",
        ),
        ("restore_drill", "failover_drill"),
    ),
    DecisionSpec(
        "provider_and_topology",
        "Provider and topology",
        ("operations",),
        ("provider", "postgres_major", "topology", "regions", "failover_mechanism"),
        ("provider_acceptance_receipt",),
    ),
    DecisionSpec(
        "enforcement_boundary",
        "Enforcement boundary",
        ("security", "engineering"),
        ("mutation_boundary", "runtime_grants"),
        ("least_privilege_proof",),
    ),
    DecisionSpec(
        "migration_posture",
        "Migration posture",
        ("release", "operations"),
        ("migration_mode", "mixed_version_window", "rollback_read_only_plan"),
        ("exact_image_rehearsal",),
    ),
    DecisionSpec(
        "capacity_and_retention",
        "Capacity and retention",
        ("product", "legal", "operations"),
        (
            "lineage_cap",
            "receipt_cap",
            "cleanup_cadence",
            "backup_retention",
            "wal_retention",
            "rpo",
            "rto",
            "budget",
        ),
        ("capacity_receipt", "recovery_receipt"),
    ),
)
SOURCE_TABLE_DETAILS = {
    "quota_policy": (
        "Dimensions, numeric limits, tier mapping, limit-change behavior, and whether tombstones/receipts consume user-visible quota",
        "Product, billing, operations; concurrency and UI receipts",
    ),
    "logical_bytes": (
        "Versioned candidate above, canonicalization changes, compressed-input treatment, explicit accept/reject flag, and conditional pre/post-decompression limits",
        "Product/security/engineering; cross-provider byte vectors",
    ),
    "recreation_and_undo": (
        "Whether IDs may be recreated, who may request it, and visible recovery promise",
        "Product; lifecycle/roaming tests",
    ),
    "offline_compatibility": (
        "Maximum client age, operation-ID lifetime, and write-disable behavior for older clients",
        "Product/support; client telemetry and replay tests",
    ),
    "tombstone_privacy_policy": (
        "Tombstone, purge, legal-hold, erasure, an explicit physical-lineage-erasure flag, independent-lineage reuse fencing, and owner-visible `410` behavior",
        "Legal/privacy/product; approved retention matrix",
    ),
    "stable_owner_identity": (
        "Stable ID or versioned key aliases, HMAC/KMS posture, and rotation/recovery",
        "Security/identity; rotation and enumeration review",
    ),
    "writer_epoch": (
        "External authority, allocation, rotation, fencing, and outage behavior",
        "Operations/security; split-writer negative proof",
    ),
    "delete_replay_and_rpo": (
        "Independent deletion-event durability, replay checkpoint, acknowledged-delete loss tolerance, ledger retention, and retention safety margin",
        "Legal/product/operations; restore/failover drill",
    ),
    "provider_and_topology": (
        "Provider, PostgreSQL major, single/standby/other topology, regions, and failover mechanism",
        "Operations; provider-specific acceptance receipts",
    ),
    "enforcement_boundary": (
        "Direct DML versus mutation procedures and corresponding runtime grants",
        "Security/engineering; least-privilege proof",
    ),
    "migration_posture": (
        "Stop-the-world versus phased backfill, mixed-version window, rollback/read-only plan",
        "Release/operations; exact-image rehearsal",
    ),
    "capacity_and_retention": (
        "Lineage/receipt caps, cleanup cadence, backup/WAL retention, RPO/RTO, budget",
        "Product/legal/operations; capacity and recovery receipts",
    ),
}
EVIDENCE_CONTRACT_NAMES = {
    evidence_kind: f"chummer.hosted_build_v002.evidence.{evidence_kind}.v1"
    for spec in DECISION_SPECS
    for evidence_kind in spec.evidence_kinds
}
EVIDENCE_PROOF_ARTIFACT_TYPES = {
    "concurrency_receipt": (
        "concurrency_test_report",
        "database_invariant_report",
    ),
    "ui_receipt": ("ui_journey_report", "ui_capture_index"),
    "cross_provider_byte_vectors": (
        "byte_vector_set",
        "provider_comparison_report",
    ),
    "lifecycle_tests": ("lifecycle_test_report",),
    "roaming_tests": ("roaming_test_report",),
    "client_telemetry": ("client_age_telemetry_report",),
    "replay_tests": ("operation_replay_test_report",),
    "approved_retention_matrix": ("signed_retention_matrix",),
    "rotation_review": ("identity_rotation_review",),
    "enumeration_review": ("identity_enumeration_review",),
    "split_writer_negative_proof": (
        "split_writer_test_report",
        "epoch_authority_receipt",
    ),
    "restore_drill": (
        "restore_drill_report",
        "deletion_replay_reconciliation_report",
    ),
    "failover_drill": (
        "failover_drill_report",
        "old_writer_fencing_report",
    ),
    "provider_acceptance_receipt": ("provider_acceptance_report",),
    "least_privilege_proof": (
        "runtime_grant_report",
        "direct_mutation_negative_test",
    ),
    "exact_image_rehearsal": (
        "exact_image_rehearsal_report",
        "release_image_digest_report",
    ),
    "capacity_receipt": ("capacity_test_report",),
    "recovery_receipt": (
        "recovery_drill_report",
        "rpo_rto_measurement_report",
    ),
}
NOT_APPLICABLE_FACETS = {
    ("logical_bytes", "compressed_pre_decompression_limit"),
    ("logical_bytes", "compressed_post_decompression_limit"),
    ("tombstone_privacy_policy", "independent_lineage_reuse_fence"),
}
FACET_VALUE_KINDS = {
    ("quota_policy", "dimensions"): "identifier_list",
    ("quota_policy", "numeric_limits"): "quota_limit_map",
    ("quota_policy", "tier_mapping"): "identifier_map",
    ("quota_policy", "limit_change_behavior"): "identifier",
    ("quota_policy", "tombstone_receipt_user_visible_accounting"): "boolean_map",
    ("logical_bytes", "algorithm"): "identifier",
    ("logical_bytes", "algorithm_version"): "positive_integer",
    ("logical_bytes", "canonicalization_change_policy"): "identifier",
    ("logical_bytes", "compressed_input_treatment"): "identifier",
    ("logical_bytes", "compressed_input_accepted"): "boolean",
    ("logical_bytes", "compressed_pre_decompression_limit"): "bytes",
    ("logical_bytes", "compressed_post_decompression_limit"): "bytes",
    ("recreation_and_undo", "id_recreation_policy"): "identifier",
    ("recreation_and_undo", "authorized_requesters"): "identifier_list",
    ("recreation_and_undo", "visible_recovery_promise"): "identifier",
    ("offline_compatibility", "maximum_client_age"): "duration",
    ("offline_compatibility", "operation_id_lifetime"): "duration",
    ("offline_compatibility", "older_client_write_disable_behavior"): "identifier",
    ("offline_compatibility", "expired_operation_id_reuse_fence"): "identifier",
    ("tombstone_privacy_policy", "tombstone_retention"): "duration",
    ("tombstone_privacy_policy", "purge_policy"): "identifier",
    ("tombstone_privacy_policy", "legal_hold_policy"): "identifier",
    ("tombstone_privacy_policy", "erasure_policy"): "identifier",
    ("tombstone_privacy_policy", "physical_lineage_erasure_required"): "boolean",
    ("tombstone_privacy_policy", "owner_visible_gone_behavior"): "identifier",
    ("tombstone_privacy_policy", "independent_lineage_reuse_fence"): "identifier",
    ("stable_owner_identity", "identity_model"): "identifier",
    ("stable_owner_identity", "key_alias_versioning"): "identifier",
    ("stable_owner_identity", "hmac_kms_posture"): "identifier",
    ("stable_owner_identity", "rotation_recovery"): "identifier",
    ("writer_epoch", "external_authority"): "identifier",
    ("writer_epoch", "allocation"): "identifier",
    ("writer_epoch", "rotation"): "identifier",
    ("writer_epoch", "fencing"): "identifier",
    ("writer_epoch", "outage_behavior"): "identifier",
    ("delete_replay_and_rpo", "independent_event_durability"): "identifier",
    ("delete_replay_and_rpo", "replay_checkpoint"): "identifier",
    ("delete_replay_and_rpo", "acknowledged_delete_loss_tolerance"): "duration",
    ("delete_replay_and_rpo", "ledger_retention"): "duration",
    ("delete_replay_and_rpo", "safety_margin"): "duration",
    ("provider_and_topology", "provider"): "identifier",
    ("provider_and_topology", "postgres_major"): "positive_integer",
    ("provider_and_topology", "topology"): "identifier",
    ("provider_and_topology", "regions"): "identifier_list",
    ("provider_and_topology", "failover_mechanism"): "identifier",
    ("enforcement_boundary", "mutation_boundary"): "identifier",
    ("enforcement_boundary", "runtime_grants"): "identifier_list",
    ("migration_posture", "migration_mode"): "identifier",
    ("migration_posture", "mixed_version_window"): "duration",
    ("migration_posture", "rollback_read_only_plan"): "identifier",
    ("capacity_and_retention", "lineage_cap"): "positive_integer",
    ("capacity_and_retention", "receipt_cap"): "positive_integer",
    ("capacity_and_retention", "cleanup_cadence"): "duration",
    ("capacity_and_retention", "backup_retention"): "duration",
    ("capacity_and_retention", "wal_retention"): "duration",
    ("capacity_and_retention", "rpo"): "duration",
    ("capacity_and_retention", "rto"): "duration",
    ("capacity_and_retention", "budget"): "money",
}
IDENTIFIER_PATTERN = re.compile(r"^[a-z][a-z0-9_]{1,63}$")


def _canonical_value(value: Any) -> Any:
    if isinstance(value, str):
        return unicodedata.normalize("NFC", value)
    if isinstance(value, list):
        return [_canonical_value(item) for item in value]
    if isinstance(value, dict):
        return {key: _canonical_value(value[key]) for key in sorted(value)}
    return value


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(
        _canonical_value(value),
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def digest_bytes(value: bytes) -> str:
    return "sha256:" + hashlib.sha256(value).hexdigest()


def _strict_json_loads(value: bytes) -> Any:
    def object_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, item in pairs:
            if key in result:
                raise ValueError("duplicate_json_key")
            result[key] = item
        return result

    def invalid_constant(_: str) -> Any:
        raise ValueError("non_finite_json_number")

    return json.loads(
        value,
        object_pairs_hook=object_pairs,
        parse_constant=invalid_constant,
    )


def _decision_digest_material(
    source_sha256: str,
    spec: DecisionSpec,
    resolution: dict[str, Any],
    *,
    include_evidence: bool,
) -> dict[str, Any]:
    material: dict[str, Any] = {
        "sourceContractSha256": source_sha256,
        "id": spec.decision_id,
        "title": spec.title,
        "requiredOwnerRoles": list(spec.owner_roles),
        "requiredAnswerFacets": list(spec.answer_facets),
        "requiredAnswerSchema": {
            facet: FACET_VALUE_KINDS[(spec.decision_id, facet)]
            for facet in spec.answer_facets
        },
        "requiredEvidenceKinds": list(spec.evidence_kinds),
        "requiredEvidenceProofArtifacts": {
            evidence_kind: list(EVIDENCE_PROOF_ARTIFACT_TYPES[evidence_kind])
            for evidence_kind in spec.evidence_kinds
        },
        "decisionStatus": resolution.get("decisionStatus"),
        "accountableOwner": resolution.get("accountableOwner"),
        "answers": resolution.get("answers"),
        "resolutionRationale": resolution.get("resolutionRationale"),
    }
    if include_evidence:
        material["evidenceRefs"] = resolution.get("evidenceRefs")
    return material


def decision_content_digest(
    source_sha256: str,
    spec: DecisionSpec,
    resolution: dict[str, Any],
) -> str:
    return digest_bytes(
        canonical_bytes(
            _decision_digest_material(
                source_sha256,
                spec,
                resolution,
                include_evidence=False,
            )
        )
    )


def decision_digest(
    source_sha256: str,
    spec: DecisionSpec,
    resolution: dict[str, Any],
) -> str:
    return digest_bytes(
        canonical_bytes(
            _decision_digest_material(
                source_sha256,
                spec,
                resolution,
                include_evidence=True,
            )
        )
    )


def _now_iso() -> str:
    return datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def _parse_utc(value: Any) -> datetime | None:
    if not isinstance(value, str) or not UTC_PATTERN.fullmatch(value):
        return None
    try:
        return datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=UTC)
    except ValueError:
        return None


def _safe_text(value: Any, *, maximum: int = 4000) -> bool:
    return (
        isinstance(value, str)
        and bool(value)
        and value == value.strip()
        and len(value) <= maximum
        and unicodedata.normalize("NFC", value) == value
        and not any(unicodedata.category(character) == "Cc" for character in value)
    )


def _explicit_text(value: Any, *, maximum: int = 4000) -> bool:
    return _safe_text(value, maximum=maximum) and value.casefold() not in PLACEHOLDER_TOKENS


def _identifier(value: Any) -> bool:
    return (
        isinstance(value, str)
        and IDENTIFIER_PATTERN.fullmatch(value) is not None
        and value.casefold() not in PLACEHOLDER_TOKENS
    )


def _typed_policy_value(value: Any, expected_kind: str) -> bool:
    if not isinstance(value, dict) or value.get("kind") != expected_kind:
        return False
    if expected_kind == "identifier":
        return _exact_keys(value, {"kind", "value"}) and _identifier(value.get("value"))
    if expected_kind == "identifier_list":
        items = value.get("value")
        return (
            _exact_keys(value, {"kind", "value"})
            and isinstance(items, list)
            and bool(items)
            and all(_identifier(item) for item in items)
            and len(items) == len(set(items))
        )
    if expected_kind == "identifier_map":
        mapping = value.get("value")
        return (
            _exact_keys(value, {"kind", "value"})
            and isinstance(mapping, dict)
            and bool(mapping)
            and all(_identifier(key) and _identifier(item) for key, item in mapping.items())
        )
    if expected_kind == "boolean_map":
        mapping = value.get("value")
        return (
            _exact_keys(value, {"kind", "value"})
            and isinstance(mapping, dict)
            and bool(mapping)
            and all(_identifier(key) and isinstance(item, bool) for key, item in mapping.items())
        )
    if expected_kind == "boolean":
        return (
            _exact_keys(value, {"kind", "value"})
            and isinstance(value.get("value"), bool)
        )
    if expected_kind == "positive_integer":
        number = value.get("value")
        return (
            _exact_keys(value, {"kind", "value"})
            and isinstance(number, int)
            and not isinstance(number, bool)
            and 0 < number <= MAX_POLICY_INTEGER
        )
    if expected_kind == "bytes":
        number = value.get("value")
        return (
            _exact_keys(value, {"kind", "value", "unit"})
            and value.get("unit") == "bytes"
            and isinstance(number, int)
            and not isinstance(number, bool)
            and 0 < number <= MAX_POLICY_INTEGER
        )
    if expected_kind == "duration":
        number = value.get("value")
        return (
            _exact_keys(value, {"kind", "value", "unit"})
            and value.get("unit") in {"seconds", "minutes", "hours", "days"}
            and isinstance(number, int)
            and not isinstance(number, bool)
            and 0 < number <= MAX_POLICY_INTEGER
        )
    if expected_kind == "quota_limit_map":
        limits = value.get("value")
        if not _exact_keys(value, {"kind", "value"}) or not isinstance(limits, dict) or not limits:
            return False
        for dimension, limit in limits.items():
            if not _identifier(dimension) or not isinstance(limit, dict):
                return False
            if not _exact_keys(limit, {"mode", "value"}):
                return False
            mode = limit.get("mode")
            number = limit.get("value")
            if mode == "limited":
                if (
                    not isinstance(number, int)
                    or isinstance(number, bool)
                    or not 0 < number <= MAX_POLICY_INTEGER
                ):
                    return False
            elif mode == "unlimited":
                if number is not None:
                    return False
            else:
                return False
        return True
    if expected_kind == "money":
        amount = value.get("amountMinor")
        currency = value.get("currency")
        return (
            _exact_keys(value, {"kind", "amountMinor", "currency"})
            and isinstance(amount, int)
            and not isinstance(amount, bool)
            and 0 <= amount <= MAX_POLICY_INTEGER
            and isinstance(currency, str)
            and re.fullmatch(r"[A-Z]{3}", currency) is not None
        )
    return False


def _exact_keys(value: Any, expected: Iterable[str]) -> bool:
    return isinstance(value, dict) and set(value) == set(expected)


def _approved_answers_by_id(decisions: list[Any]) -> dict[str, dict[str, Any]]:
    answers_by_id: dict[str, dict[str, Any]] = {}
    for decision in decisions:
        if not isinstance(decision, dict):
            continue
        decision_id = decision.get("id")
        resolution = decision.get("resolution")
        if (
            isinstance(decision_id, str)
            and isinstance(resolution, dict)
            and resolution.get("decisionStatus") == "approved"
            and isinstance(resolution.get("answers"), dict)
        ):
            answers_by_id[decision_id] = resolution["answers"]
    return answers_by_id


def _selected_typed_value(
    answers_by_id: dict[str, dict[str, Any]],
    decision_id: str,
    facet: str,
) -> dict[str, Any] | None:
    answer = answers_by_id.get(decision_id, {}).get(facet)
    expected_kind = FACET_VALUE_KINDS.get((decision_id, facet))
    if (
        not isinstance(answer, dict)
        or answer.get("disposition") != "selected"
        or expected_kind is None
        or not _typed_policy_value(answer.get("value"), expected_kind)
    ):
        return None
    return answer["value"]


def _duration_seconds(value: dict[str, Any] | None) -> int | None:
    if value is None or not _typed_policy_value(value, "duration"):
        return None
    multipliers = {
        "seconds": 1,
        "minutes": 60,
        "hours": 60 * 60,
        "days": 24 * 60 * 60,
    }
    return value["value"] * multipliers[value["unit"]]


def _cross_decision_policy_errors(
    decisions: list[Any],
) -> tuple[list[str], set[str]]:
    errors: list[str] = []
    invalid_ids: set[str] = set()
    answers_by_id = _approved_answers_by_id(decisions)

    logical_answers = answers_by_id.get("logical_bytes")
    compressed_input_accepted = _selected_typed_value(
        answers_by_id,
        "logical_bytes",
        "compressed_input_accepted",
    )
    if logical_answers is not None and compressed_input_accepted is not None:
        limit_dispositions = [
            (
                logical_answers.get(facet, {}).get("disposition")
                if isinstance(logical_answers.get(facet), dict)
                else None
            )
            for facet in (
                "compressed_pre_decompression_limit",
                "compressed_post_decompression_limit",
            )
        ]
        if compressed_input_accepted["value"] is False:
            if limit_dispositions != ["not_applicable", "not_applicable"]:
                errors.append(
                    "logical_bytes:cross_policy:reject_requires_compression_limits_not_applicable"
                )
                invalid_ids.add("logical_bytes")
        elif limit_dispositions != ["selected", "selected"]:
            errors.append(
                "logical_bytes:cross_policy:accepted_compression_requires_both_limits"
            )
            invalid_ids.add("logical_bytes")

    tombstone_answers = answers_by_id.get("tombstone_privacy_policy")
    physical_lineage_erasure_required = _selected_typed_value(
        answers_by_id,
        "tombstone_privacy_policy",
        "physical_lineage_erasure_required",
    )
    if (
        tombstone_answers is not None
        and physical_lineage_erasure_required is not None
        and physical_lineage_erasure_required["value"] is True
    ):
        reuse_fence = tombstone_answers.get("independent_lineage_reuse_fence")
        if not isinstance(reuse_fence, dict) or reuse_fence.get("disposition") != "selected":
            errors.append(
                "tombstone_privacy_policy:cross_policy:physical_erasure_requires_reuse_fence"
            )
            invalid_ids.add("tombstone_privacy_policy")

    maximum_client_age = _duration_seconds(
        _selected_typed_value(
            answers_by_id,
            "offline_compatibility",
            "maximum_client_age",
        )
    )
    operation_id_lifetime = _duration_seconds(
        _selected_typed_value(
            answers_by_id,
            "offline_compatibility",
            "operation_id_lifetime",
        )
    )
    if (
        maximum_client_age is not None
        and operation_id_lifetime is not None
        and operation_id_lifetime < maximum_client_age
    ):
        errors.append(
            "offline_compatibility:cross_policy:operation_id_lifetime_shorter_than_maximum_client_age"
        )
        invalid_ids.add("offline_compatibility")

    backup_retention = _duration_seconds(
        _selected_typed_value(
            answers_by_id,
            "capacity_and_retention",
            "backup_retention",
        )
    )
    wal_retention = _duration_seconds(
        _selected_typed_value(
            answers_by_id,
            "capacity_and_retention",
            "wal_retention",
        )
    )
    safety_margin = _duration_seconds(
        _selected_typed_value(
            answers_by_id,
            "delete_replay_and_rpo",
            "safety_margin",
        )
    )
    if (
        backup_retention is not None
        and wal_retention is not None
        and safety_margin is not None
    ):
        minimum_retention = max(backup_retention, wal_retention) + safety_margin
        ledger_retention = _duration_seconds(
            _selected_typed_value(
                answers_by_id,
                "delete_replay_and_rpo",
                "ledger_retention",
            )
        )
        if ledger_retention is not None and ledger_retention < minimum_retention:
            errors.append(
                "delete_replay_and_rpo:cross_policy:ledger_retention_below_recoverable_window_plus_safety_margin"
            )
            invalid_ids.update({"delete_replay_and_rpo", "capacity_and_retention"})
        tombstone_retention = _duration_seconds(
            _selected_typed_value(
                answers_by_id,
                "tombstone_privacy_policy",
                "tombstone_retention",
            )
        )
        if tombstone_retention is not None and tombstone_retention < minimum_retention:
            errors.append(
                "tombstone_privacy_policy:cross_policy:tombstone_retention_below_recoverable_window_plus_safety_margin"
            )
            invalid_ids.update(
                {
                    "tombstone_privacy_policy",
                    "delete_replay_and_rpo",
                    "capacity_and_retention",
                }
            )
    return errors, invalid_ids


def _source_decision_table_matches(source_bytes: bytes) -> bool:
    try:
        source = source_bytes.decode("utf-8", errors="strict")
    except UnicodeDecodeError:
        return False
    start_marker = "## Unresolved operator decision table"
    end_marker = "## Machine-verifiable operator decision gate"
    if source.count(start_marker) != 1 or source.count(end_marker) != 1:
        return False
    table = source.split(start_marker, 1)[1].split(end_marker, 1)[0]
    rows: list[tuple[str, str, str, str]] = []
    for line in table.splitlines():
        cells = [cell.strip() for cell in line.split("|")]
        if len(cells) != 6 or not re.fullmatch(r"`[a-z0-9_]+`", cells[1]):
            continue
        rows.append(
            (
                cells[1][1:-1],
                cells[2],
                cells[3],
                cells[4],
            )
        )
    expected_rows = [
        (
            spec.decision_id,
            spec.title,
            SOURCE_TABLE_DETAILS[spec.decision_id][0],
            SOURCE_TABLE_DETAILS[spec.decision_id][1],
        )
        for spec in DECISION_SPECS
    ]
    return rows == expected_rows


def approval_registry_signing_material(payload: dict[str, Any]) -> dict[str, Any]:
    return {
        "contractName": payload.get("contractName"),
        "contractVersion": payload.get("contractVersion"),
        "status": payload.get("status"),
        "keys": payload.get("keys"),
    }


def approval_root_key_id(public_key_bytes: bytes) -> str:
    return "root-" + hashlib.sha256(public_key_bytes).hexdigest()[:32]


def _approval_root_public_key_from_environment() -> tuple[bytes | None, str | None]:
    encoded = os.environ.get(APPROVAL_ROOT_PUBLIC_KEY_ENV)
    if encoded is None:
        return None, None
    try:
        public_key_bytes = base64.b64decode(encoded, validate=True)
    except (ValueError, binascii.Error):
        return None, "approval_root_public_key_environment_invalid"
    if len(public_key_bytes) != 32:
        return None, "approval_root_public_key_environment_invalid"
    return public_key_bytes, None


def _validate_registry_root_authorization(
    payload: dict[str, Any],
    root_public_key: bytes | None,
    observed_at: datetime,
) -> list[str]:
    authorization = payload.get("rootAuthorization")
    if payload.get("status") == "unconfigured":
        return [] if authorization is None else [
            "unconfigured_approval_key_registry_root_authorization_must_be_null"
        ]
    if not _exact_keys(
        authorization,
        {
            "authority",
            "rootKeyId",
            "signedAtUtc",
            "registryContentSha256",
            "signatureBase64",
        },
    ):
        return ["approval_key_registry_root_authorization_invalid"]
    if root_public_key is None:
        return ["approval_key_registry_root_key_unavailable"]
    expected_root_key_id = approval_root_key_id(root_public_key)
    expected_content_digest = digest_bytes(
        canonical_bytes(approval_registry_signing_material(payload))
    )
    signed_at = _parse_utc(authorization.get("signedAtUtc"))
    if (
        authorization.get("authority") != "external_ed25519_root"
        or authorization.get("rootKeyId") != expected_root_key_id
        or authorization.get("registryContentSha256") != expected_content_digest
        or signed_at is None
        or signed_at > observed_at
    ):
        return ["approval_key_registry_root_authorization_unbound"]
    if observed_at - signed_at > MAX_ROOT_AUTHORIZATION_AGE:
        return ["approval_key_registry_root_authorization_stale"]
    try:
        signature = base64.b64decode(
            authorization.get("signatureBase64"),
            validate=True,
        )
    except (TypeError, ValueError, binascii.Error):
        return ["approval_key_registry_root_signature_invalid"]
    if len(signature) != 64 or Ed25519PublicKey is None:
        return ["approval_key_registry_root_signature_invalid"]
    signed_payload = dict(authorization)
    signed_payload.pop("signatureBase64", None)
    try:
        Ed25519PublicKey.from_public_bytes(root_public_key).verify(
            signature,
            canonical_bytes(signed_payload),
        )
    except (InvalidSignature, ValueError):
        return ["approval_key_registry_root_signature_invalid"]
    return []


def _validate_approval_key_registry(
    payload: Any,
    *,
    root_public_key: bytes | None,
    observed_at: datetime,
    actual_registry_sha256: str,
    externally_pinned_registry_sha256: str | None,
) -> tuple[list[str], dict[str, dict[str, Any]], str | None]:
    errors: list[str] = []
    trusted_keys: dict[str, dict[str, Any]] = {}
    seen_key_ids: set[str] = set()
    seen_public_key_digests: set[str] = set()
    if not _exact_keys(
        payload,
        {"contractName", "contractVersion", "status", "keys", "rootAuthorization"},
    ):
        return ["approval_key_registry_shape_invalid"], {}, None
    if payload.get("contractName") != APPROVAL_KEY_REGISTRY_CONTRACT_NAME:
        errors.append("approval_key_registry_contract_invalid")
    version = payload.get("contractVersion")
    if not isinstance(version, int) or isinstance(version, bool) or version != 1:
        errors.append("approval_key_registry_version_invalid")
    registry_status = payload.get("status")
    if registry_status not in {"active", "unconfigured"}:
        errors.append("approval_key_registry_status_invalid")
    keys = payload.get("keys")
    if not isinstance(keys, list):
        errors.append("approval_key_registry_keys_invalid")
        keys = []
    if registry_status == "unconfigured" and keys:
        errors.append("unconfigured_approval_key_registry_must_be_empty")
    if registry_status == "active" and not keys:
        errors.append("active_approval_key_registry_must_not_be_empty")
    if registry_status == "active":
        if externally_pinned_registry_sha256 is None:
            errors.append("approval_key_registry_external_digest_unavailable")
        elif externally_pinned_registry_sha256 != actual_registry_sha256:
            errors.append("approval_key_registry_external_digest_mismatch")
    errors.extend(
        _validate_registry_root_authorization(
            payload,
            root_public_key,
            observed_at,
        )
    )
    allowed_roles = {
        role
        for spec in DECISION_SPECS
        for role in spec.owner_roles
    }
    for key in keys:
        if not _exact_keys(
            key,
            {
                "keyId",
                "algorithm",
                "publicKeyBase64",
                "roles",
                "actorIds",
                "status",
            },
        ):
            errors.append("approval_key_shape_invalid")
            continue
        key_id = key.get("keyId")
        roles = key.get("roles")
        actor_ids = key.get("actorIds")
        if (
            not isinstance(key_id, str)
            or not ACTOR_PATTERN.fullmatch(key_id)
            or key_id in seen_key_ids
        ):
            errors.append("approval_key_id_invalid_or_duplicate")
            continue
        seen_key_ids.add(key_id)
        if key.get("algorithm") != "ed25519":
            errors.append(f"approval_key:{key_id}:algorithm_invalid")
        if key.get("status") not in {"active", "revoked"}:
            errors.append(f"approval_key:{key_id}:status_invalid")
        if (
            not isinstance(roles, list)
            or not roles
            or any(not isinstance(role, str) for role in roles)
            or any(role not in allowed_roles for role in roles)
            or len(roles) != len(set(roles))
        ):
            errors.append(f"approval_key:{key_id}:roles_invalid")
        if (
            not isinstance(actor_ids, list)
            or not actor_ids
            or any(
                not isinstance(actor_id, str)
                or not ACTOR_PATTERN.fullmatch(actor_id)
                or "@" in actor_id
                for actor_id in actor_ids
            )
            or len(actor_ids) != len(set(actor_ids))
        ):
            errors.append(f"approval_key:{key_id}:actor_ids_invalid")
        try:
            public_key_bytes = base64.b64decode(
                key.get("publicKeyBase64"),
                validate=True,
            )
        except (TypeError, ValueError, binascii.Error):
            public_key_bytes = b""
        if len(public_key_bytes) != 32:
            errors.append(f"approval_key:{key_id}:public_key_invalid")
        public_key_digest = (
            digest_bytes(public_key_bytes)
            if len(public_key_bytes) == 32
            else None
        )
        public_key_unique = public_key_digest not in seen_public_key_digests
        if public_key_digest is not None:
            if not public_key_unique:
                errors.append(f"approval_key:{key_id}:public_key_duplicate")
            seen_public_key_digests.add(public_key_digest)
        if (
            key.get("status") == "active"
            and len(public_key_bytes) == 32
            and public_key_unique
        ):
            trusted_keys[key_id] = {
                "public_key": public_key_bytes,
                "roles": tuple(roles) if isinstance(roles, list) else (),
                "actor_ids": tuple(actor_ids) if isinstance(actor_ids, list) else (),
            }
    if registry_status == "active" and not trusted_keys:
        errors.append("approval_key_registry_has_no_active_keys")
    if any(
        error.startswith("approval_key_registry_root_")
        or error.startswith("approval_key_registry_external_")
        for error in errors
    ):
        trusted_keys = {}
    return list(dict.fromkeys(errors)), trusted_keys, registry_status


def _repo_root(workspace_root: Path, repo: str) -> Path | None:
    if repo in {"chummer-presentation", "chummer.run-services", "chummer-hub-registry"}:
        return workspace_root / repo
    if repo == "fleet":
        return workspace_root.parent / "fleet"
    return None


def _valid_relative_path(value: Any) -> bool:
    if not isinstance(value, str) or not value or "\\" in value or "\x00" in value:
        return False
    path = PurePosixPath(value)
    return (
        not path.is_absolute()
        and path.as_posix() == value
        and all(part not in {"", ".", ".."} for part in path.parts)
    )


def _read_repo_file(
    workspace_root: Path,
    repo: str,
    relative_path: str,
    maximum_bytes: int,
) -> bytes:
    root = _repo_root(workspace_root, repo)
    if root is None or not _valid_relative_path(relative_path):
        raise ValueError("path_not_allowlisted")
    parts = PurePosixPath(relative_path).parts
    flags = os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0)
    directory_flags = flags | getattr(os, "O_DIRECTORY", 0)
    descriptors: list[int] = []
    try:
        current = os.open(root, directory_flags)
        descriptors.append(current)
        for component in parts[:-1]:
            current = os.open(component, directory_flags, dir_fd=current)
            descriptors.append(current)
        file_descriptor = os.open(parts[-1], flags, dir_fd=current)
        descriptors.append(file_descriptor)
        file_stat = os.fstat(file_descriptor)
        if not stat.S_ISREG(file_stat.st_mode) or file_stat.st_size > maximum_bytes:
            raise ValueError("file_not_bounded_regular")
        chunks: list[bytes] = []
        remaining = maximum_bytes + 1
        while remaining > 0:
            chunk = os.read(file_descriptor, min(65536, remaining))
            if not chunk:
                break
            chunks.append(chunk)
            remaining -= len(chunk)
        data = b"".join(chunks)
        if len(data) > maximum_bytes:
            raise ValueError("file_too_large")
        return data
    except OSError as error:
        raise ValueError("file_unavailable_or_unsafe") from error
    finally:
        for descriptor in reversed(descriptors):
            try:
                os.close(descriptor)
            except OSError:
                pass


def _validate_evidence_proof_artifact(
    decision_id: str,
    evidence_kind: str,
    expected_artifact_type: str,
    artifact: Any,
    workspace_root: Path,
) -> list[str]:
    prefix = (
        f"{decision_id}:evidence:{evidence_kind}:proof:{expected_artifact_type}"
    )
    if not _exact_keys(
        artifact,
        {"artifactType", "repo", "path", "sha256", "byteCount"},
    ):
        return [f"{prefix}:shape_invalid"]
    errors: list[str] = []
    if artifact.get("artifactType") != expected_artifact_type:
        errors.append(f"{prefix}:artifact_type_invalid")
    repo = artifact.get("repo")
    relative_path = artifact.get("path")
    expected_digest = artifact.get("sha256")
    expected_byte_count = artifact.get("byteCount")
    if not isinstance(repo, str) or _repo_root(workspace_root, repo) is None:
        errors.append(f"{prefix}:repo_not_allowlisted")
    if (
        not _valid_relative_path(relative_path)
        or not relative_path.startswith(EVIDENCE_PROOF_ARTIFACT_PATH_PREFIX)
    ):
        errors.append(f"{prefix}:path_invalid")
    if not isinstance(expected_digest, str) or not SHA256_PATTERN.fullmatch(
        expected_digest
    ):
        errors.append(f"{prefix}:digest_invalid")
    if (
        not isinstance(expected_byte_count, int)
        or isinstance(expected_byte_count, bool)
        or not 0 < expected_byte_count <= MAX_EVIDENCE_BYTES
    ):
        errors.append(f"{prefix}:byte_count_invalid")
    if errors:
        return errors
    try:
        artifact_bytes = _read_repo_file(
            workspace_root,
            repo,
            relative_path,
            MAX_EVIDENCE_BYTES,
        )
    except ValueError as error:
        return [f"{prefix}:{error}"]
    if len(artifact_bytes) != expected_byte_count:
        errors.append(f"{prefix}:byte_count_mismatch")
    if digest_bytes(artifact_bytes) != expected_digest:
        errors.append(f"{prefix}:digest_mismatch")
    return errors


def _proof_artifact_files_are_distinct(proof_artifacts: Any) -> bool:
    if not isinstance(proof_artifacts, list) or not proof_artifacts:
        return False
    if any(not isinstance(artifact, dict) for artifact in proof_artifacts):
        return False
    paths = [artifact.get("path") for artifact in proof_artifacts]
    digests = [artifact.get("sha256") for artifact in proof_artifacts]
    return (
        all(isinstance(path, str) for path in paths)
        and len(paths) == len(set(paths))
        and all(isinstance(digest, str) for digest in digests)
        and len(digests) == len(set(digests))
    )


def _evidence_payload_is_clear(
    payload: Any,
    *,
    contract_name: str,
    evidence_kind: str,
    source_sha256: str,
    decision_sha256: str,
    release_identity: str | None,
    observed_at: datetime,
) -> bool:
    if not isinstance(payload, dict):
        return False
    required_keys = {
        "contractName",
        "contractVersion",
        "evidenceKind",
        "status",
        "reviewRequired",
        "blockers",
        "sourceContractSha256",
        "decisionSha256",
        "generatedAtUtc",
        "releaseIdentity",
        "producer",
        "proofArtifacts",
    }
    if set(payload) != required_keys:
        return False
    if payload.get("contractName") != contract_name:
        return False
    contract_version = payload.get("contractVersion")
    generated_at = _parse_utc(payload.get("generatedAtUtc"))
    producer = payload.get("producer")
    proof_artifacts = payload.get("proofArtifacts")
    expected_artifact_types = EVIDENCE_PROOF_ARTIFACT_TYPES.get(evidence_kind)
    return (
        isinstance(contract_version, int)
        and not isinstance(contract_version, bool)
        and contract_version == 1
        and payload.get("evidenceKind") == evidence_kind
        and payload.get("status") == "pass"
        and payload.get("reviewRequired") is False
        and payload.get("blockers") == []
        and payload.get("sourceContractSha256") == source_sha256
        and payload.get("decisionSha256") == decision_sha256
        and generated_at is not None
        and generated_at <= observed_at
        and observed_at - generated_at <= MAX_EVIDENCE_AGE
        and payload.get("releaseIdentity") == release_identity
        and _exact_keys(
            producer,
            {"name", "version", "runId", "invocationSha256"},
        )
        and _identifier(producer.get("name"))
        and _safe_text(producer.get("version"), maximum=80)
        and _safe_text(producer.get("runId"), maximum=160)
        and isinstance(producer.get("invocationSha256"), str)
        and SHA256_PATTERN.fullmatch(producer["invocationSha256"]) is not None
        and isinstance(proof_artifacts, list)
        and expected_artifact_types is not None
        and [
            artifact.get("artifactType") if isinstance(artifact, dict) else None
            for artifact in proof_artifacts
        ]
        == list(expected_artifact_types)
        and _proof_artifact_files_are_distinct(proof_artifacts)
    )


def _validate_evidence(
    decision_id: str,
    evidence: Any,
    expected_kind: str,
    workspace_root: Path,
    source_sha256: str,
    decision_sha256: str,
    observed_at: datetime,
    candidate_release_identity: str | None,
) -> list[str]:
    prefix = f"{decision_id}:evidence:{expected_kind}"
    errors: list[str] = []
    expected_keys = {"kind", "repo", "path", "sha256", "contractName", "releaseIdentity"}
    if not _exact_keys(evidence, expected_keys):
        return [f"{prefix}:shape_invalid"]
    if evidence.get("kind") != expected_kind:
        errors.append(f"{prefix}:kind_mismatch")
    repo = evidence.get("repo")
    relative_path = evidence.get("path")
    expected_digest = evidence.get("sha256")
    contract_name = evidence.get("contractName")
    release_identity = evidence.get("releaseIdentity")
    if not isinstance(repo, str) or _repo_root(workspace_root, repo) is None:
        errors.append(f"{prefix}:repo_not_allowlisted")
    if not _valid_relative_path(relative_path):
        errors.append(f"{prefix}:path_invalid")
    elif not relative_path.startswith(EVIDENCE_PATH_PREFIX) or not relative_path.endswith(".json"):
        errors.append(f"{prefix}:path_not_in_evidence_root")
    if not isinstance(expected_digest, str) or not SHA256_PATTERN.fullmatch(expected_digest):
        errors.append(f"{prefix}:digest_invalid")
    if contract_name != EVIDENCE_CONTRACT_NAMES[expected_kind]:
        errors.append(f"{prefix}:contract_name_invalid")
    if expected_kind in RELEASE_BOUND_EVIDENCE_KINDS:
        if (
            not _safe_text(release_identity, maximum=160)
            or release_identity != candidate_release_identity
        ):
            errors.append(f"{prefix}:release_identity_required")
    elif release_identity is not None:
        errors.append(f"{prefix}:release_identity_must_be_null")
    if errors:
        return errors
    try:
        evidence_bytes = _read_repo_file(
            workspace_root,
            repo,
            relative_path,
            MAX_EVIDENCE_BYTES,
        )
    except ValueError as error:
        return [f"{prefix}:{error}"]
    if digest_bytes(evidence_bytes) != expected_digest:
        return [f"{prefix}:digest_mismatch"]
    try:
        payload = _strict_json_loads(evidence_bytes)
    except (UnicodeDecodeError, json.JSONDecodeError, ValueError):
        return [f"{prefix}:receipt_invalid"]
    if not _evidence_payload_is_clear(
        payload,
        contract_name=contract_name,
        evidence_kind=expected_kind,
        source_sha256=source_sha256,
        decision_sha256=decision_sha256,
        release_identity=release_identity,
        observed_at=observed_at,
    ):
        return [f"{prefix}:receipt_not_clear"]
    proof_artifacts = payload["proofArtifacts"]
    proof_errors: list[str] = []
    for index, expected_artifact_type in enumerate(
        EVIDENCE_PROOF_ARTIFACT_TYPES[expected_kind]
    ):
        proof_errors.extend(
            _validate_evidence_proof_artifact(
                decision_id,
                expected_kind,
                expected_artifact_type,
                proof_artifacts[index],
                workspace_root,
            )
        )
    if proof_errors:
        return proof_errors
    if release_identity is not None:
        actual_release = payload.get("releaseIdentity", payload.get("release_identity"))
        if actual_release != release_identity:
            return [f"{prefix}:release_identity_mismatch"]
    return []


def _validate_unresolved(decision_id: str, resolution: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if resolution.get("accountableOwner") is not None:
        errors.append(f"{decision_id}:unresolved_accountable_owner_must_be_null")
    if resolution.get("answers") != {}:
        errors.append(f"{decision_id}:unresolved_answers_must_be_empty")
    if resolution.get("resolutionRationale") is not None:
        errors.append(f"{decision_id}:unresolved_rationale_must_be_null")
    if resolution.get("approvals") != []:
        errors.append(f"{decision_id}:unresolved_approvals_must_be_empty")
    if resolution.get("evidenceRefs") != []:
        errors.append(f"{decision_id}:unresolved_evidence_must_be_empty")
    return errors


def _validate_approval_attestation(
    decision_id: str,
    approval: dict[str, Any],
    *,
    workspace_root: Path,
    source_sha256: str,
    decision_sha256: str,
    observed_at: datetime,
    trusted_approval_keys: dict[str, dict[str, Any]],
) -> list[str]:
    role = approval.get("role")
    prefix = f"{decision_id}:approval:{role}"
    attestation = approval.get("attestationRef")
    if not _exact_keys(attestation, {"repo", "path", "sha256", "contractName"}):
        return [f"{prefix}:attestation_shape_invalid"]
    repo = attestation.get("repo")
    relative_path = attestation.get("path")
    expected_digest = attestation.get("sha256")
    if _repo_root(workspace_root, repo) is None:
        return [f"{prefix}:attestation_repo_not_allowlisted"]
    if (
        not _valid_relative_path(relative_path)
        or not relative_path.startswith(APPROVAL_PATH_PREFIX)
        or not relative_path.endswith(".json")
    ):
        return [f"{prefix}:attestation_path_invalid"]
    if not isinstance(expected_digest, str) or not SHA256_PATTERN.fullmatch(expected_digest):
        return [f"{prefix}:attestation_digest_invalid"]
    if attestation.get("contractName") != APPROVAL_CONTRACT_NAME:
        return [f"{prefix}:attestation_contract_invalid"]
    try:
        attestation_bytes = _read_repo_file(
            workspace_root,
            repo,
            relative_path,
            MAX_EVIDENCE_BYTES,
        )
    except ValueError as error:
        return [f"{prefix}:attestation:{error}"]
    if digest_bytes(attestation_bytes) != expected_digest:
        return [f"{prefix}:attestation_digest_mismatch"]
    try:
        payload = _strict_json_loads(attestation_bytes)
    except (UnicodeDecodeError, json.JSONDecodeError, ValueError):
        return [f"{prefix}:attestation_invalid"]
    required_keys = {
        "contractName",
        "contractVersion",
        "authority",
        "status",
        "reviewRequired",
        "blockers",
        "role",
        "actorId",
        "approvedAtUtc",
        "sourceContractSha256",
        "decisionSha256",
        "keyId",
        "signatureBase64",
    }
    contract_version = payload.get("contractVersion") if isinstance(payload, dict) else None
    approved_at = _parse_utc(payload.get("approvedAtUtc")) if isinstance(payload, dict) else None
    if (
        not isinstance(payload, dict)
        or not required_keys.issubset(payload)
        or payload.get("contractName") != APPROVAL_CONTRACT_NAME
        or not isinstance(contract_version, int)
        or isinstance(contract_version, bool)
        or contract_version != 1
        or payload.get("authority") != "ed25519_role_registry"
        or payload.get("status") != "approved"
        or payload.get("reviewRequired") is not False
        or payload.get("blockers") != []
        or payload.get("role") != role
        or payload.get("actorId") != approval.get("actorId")
        or payload.get("approvedAtUtc") != approval.get("approvedAtUtc")
        or approved_at is None
        or approved_at > observed_at
        or observed_at - approved_at > MAX_APPROVAL_AGE
        or payload.get("sourceContractSha256") != source_sha256
        or payload.get("decisionSha256") != decision_sha256
    ):
        return [f"{prefix}:attestation_not_approved_or_unbound"]
    key_id = payload.get("keyId")
    if approval.get("keyId") != key_id or key_id not in trusted_approval_keys:
        return [f"{prefix}:attestation_key_untrusted"]
    trusted_key = trusted_approval_keys[key_id]
    if role not in trusted_key["roles"]:
        return [f"{prefix}:attestation_key_role_unauthorized"]
    if approval.get("actorId") not in trusted_key["actor_ids"]:
        return [f"{prefix}:attestation_key_actor_unauthorized"]
    try:
        signature = base64.b64decode(payload.get("signatureBase64"), validate=True)
    except (TypeError, ValueError, binascii.Error):
        return [f"{prefix}:attestation_signature_invalid"]
    if len(signature) != 64 or Ed25519PublicKey is None:
        return [f"{prefix}:attestation_signature_invalid"]
    signed_payload = dict(payload)
    signed_payload.pop("signatureBase64", None)
    try:
        Ed25519PublicKey.from_public_bytes(trusted_key["public_key"]).verify(
            signature,
            canonical_bytes(signed_payload),
        )
    except (InvalidSignature, ValueError):
        return [f"{prefix}:attestation_signature_invalid"]
    return []


def _validate_approved(
    spec: DecisionSpec,
    resolution: dict[str, Any],
    source_sha256: str,
    workspace_root: Path,
    observed_at: datetime,
    candidate_release_identity: str | None,
    trusted_approval_keys: dict[str, dict[str, Any]],
    approval_registry_status: str | None,
) -> list[str]:
    decision_id = spec.decision_id
    errors: list[str] = []
    owner = resolution.get("accountableOwner")
    if not _exact_keys(owner, {"role", "actorId"}):
        errors.append(f"{decision_id}:accountable_owner_invalid")
        owner = {}
    owner_role = owner.get("role")
    owner_actor = owner.get("actorId")
    if owner_role not in spec.owner_roles:
        errors.append(f"{decision_id}:accountable_owner_role_invalid")
    if not isinstance(owner_actor, str) or not ACTOR_PATTERN.fullmatch(owner_actor) or "@" in owner_actor:
        errors.append(f"{decision_id}:accountable_owner_actor_invalid")

    answers = resolution.get("answers")
    if not isinstance(answers, dict) or set(answers) != set(spec.answer_facets):
        errors.append(f"{decision_id}:answer_facets_invalid")
        answers = {}
    for facet in spec.answer_facets:
        answer = answers.get(facet)
        if not _exact_keys(answer, {"disposition", "value", "rationale"}):
            errors.append(f"{decision_id}:answer:{facet}:shape_invalid")
            continue
        disposition = answer.get("disposition")
        if disposition == "selected":
            expected_kind = FACET_VALUE_KINDS.get((decision_id, facet))
            if expected_kind is None:
                errors.append(f"{decision_id}:answer:{facet}:value_schema_missing")
            elif not _typed_policy_value(answer.get("value"), expected_kind):
                errors.append(f"{decision_id}:answer:{facet}:selected_value_required")
        elif disposition == "not_applicable":
            if answer.get("value") is not None:
                errors.append(f"{decision_id}:answer:{facet}:not_applicable_value_must_be_null")
            if (decision_id, facet) not in NOT_APPLICABLE_FACETS:
                errors.append(f"{decision_id}:answer:{facet}:not_applicable_forbidden")
        else:
            errors.append(f"{decision_id}:answer:{facet}:disposition_invalid")
        if not _explicit_text(answer.get("rationale")):
            errors.append(f"{decision_id}:answer:{facet}:rationale_required")
    if not _explicit_text(resolution.get("resolutionRationale")):
        errors.append(f"{decision_id}:resolution_rationale_required")

    expected_content_digest = decision_content_digest(source_sha256, spec, resolution)
    expected_digest = decision_digest(source_sha256, spec, resolution)
    approvals = resolution.get("approvals")
    if not isinstance(approvals, list):
        errors.append(f"{decision_id}:approvals_invalid")
        approvals = []
    approval_roles: list[str] = []
    approval_actor_ids: list[str] = []
    approval_key_ids: list[str] = []
    for approval in approvals:
        if not _exact_keys(
            approval,
            {
                "role",
                "actorId",
                "approvedAtUtc",
                "decisionSha256",
                "keyId",
                "attestationRef",
            },
        ):
            errors.append(f"{decision_id}:approval_shape_invalid")
            continue
        role = approval.get("role")
        actor_id = approval.get("actorId")
        approved_at = _parse_utc(approval.get("approvedAtUtc"))
        approval_roles.append(str(role))
        approval_actor_ids.append(str(actor_id))
        approval_key_ids.append(str(approval.get("keyId")))
        if role not in spec.owner_roles:
            errors.append(f"{decision_id}:approval_role_invalid")
        if not isinstance(actor_id, str) or not ACTOR_PATTERN.fullmatch(actor_id) or "@" in actor_id:
            errors.append(f"{decision_id}:approval_actor_invalid")
        if (
            approved_at is None
            or approved_at > observed_at
            or observed_at - approved_at > MAX_APPROVAL_AGE
        ):
            errors.append(f"{decision_id}:approval_time_invalid")
        if approval.get("decisionSha256") != expected_digest:
            errors.append(f"{decision_id}:approval_digest_mismatch")
        if role == owner_role and actor_id != owner_actor:
            errors.append(f"{decision_id}:accountable_owner_approval_mismatch")
        errors.extend(
            _validate_approval_attestation(
                decision_id,
                approval,
                workspace_root=workspace_root,
                source_sha256=source_sha256,
                decision_sha256=expected_digest,
                observed_at=observed_at,
                trusted_approval_keys=trusted_approval_keys,
            )
        )
    if approval_roles != list(spec.owner_roles):
        errors.append(f"{decision_id}:approval_roles_invalid")
    if len(approval_actor_ids) != len(set(approval_actor_ids)):
        errors.append(f"{decision_id}:approval_actors_must_be_distinct")
    if len(approval_key_ids) != len(set(approval_key_ids)):
        errors.append(f"{decision_id}:approval_keys_must_be_distinct")
    if approval_registry_status != "active":
        errors.append(f"{decision_id}:approval_key_registry_not_active")

    evidence_refs = resolution.get("evidenceRefs")
    if not isinstance(evidence_refs, list):
        errors.append(f"{decision_id}:evidence_refs_invalid")
        evidence_refs = []
    evidence_kinds = [item.get("kind") if isinstance(item, dict) else None for item in evidence_refs]
    if evidence_kinds != list(spec.evidence_kinds):
        errors.append(f"{decision_id}:evidence_kinds_invalid")
    for index, expected_kind in enumerate(spec.evidence_kinds):
        if index < len(evidence_refs):
            errors.extend(
                _validate_evidence(
                    decision_id,
                    evidence_refs[index],
                    expected_kind,
                    workspace_root,
                    source_sha256,
                    expected_content_digest,
                    observed_at,
                    candidate_release_identity,
                )
            )
    return errors


def evaluate_packet(
    payload: Any,
    *,
    packet_bytes: bytes,
    source_bytes: bytes,
    approval_registry_payload: Any,
    approval_registry_bytes: bytes,
    workspace_root: Path,
    generated_at_utc: str,
    canonical_provenance: bool = True,
    approval_trust_root_public_key: bytes | None = None,
    approval_trust_registry_sha256: str | None = None,
) -> dict[str, Any]:
    observed_at = _parse_utc(generated_at_utc)
    if observed_at is None:
        raise ValueError("generated_at_utc must use strict UTC second precision")
    errors: list[str] = []
    invalid_ids: set[str] = set()
    unresolved_ids: list[str] = []
    approved_ids: list[str] = []
    source_sha256 = digest_bytes(source_bytes)
    approval_registry_sha256 = digest_bytes(approval_registry_bytes)
    root_key_environment_error: str | None = None
    if approval_trust_root_public_key is None:
        (
            approval_trust_root_public_key,
            root_key_environment_error,
        ) = _approval_root_public_key_from_environment()
    elif len(approval_trust_root_public_key) != 32:
        root_key_environment_error = "approval_root_public_key_invalid"
        approval_trust_root_public_key = None
    if root_key_environment_error is not None:
        errors.append(root_key_environment_error)
    if approval_trust_registry_sha256 is None:
        approval_trust_registry_sha256 = os.environ.get(
            APPROVAL_REGISTRY_SHA256_ENV
        )
    if (
        approval_trust_registry_sha256 is not None
        and SHA256_PATTERN.fullmatch(approval_trust_registry_sha256) is None
    ):
        errors.append("approval_registry_sha256_environment_invalid")
        approval_trust_registry_sha256 = None
    (
        approval_registry_errors,
        trusted_approval_keys,
        approval_registry_status,
    ) = _validate_approval_key_registry(
        approval_registry_payload,
        root_public_key=approval_trust_root_public_key,
        observed_at=observed_at,
        actual_registry_sha256=approval_registry_sha256,
        externally_pinned_registry_sha256=approval_trust_registry_sha256,
    )
    errors.extend(approval_registry_errors)

    if not canonical_provenance:
        errors.append("canonical_provenance_required")
    if not _source_decision_table_matches(source_bytes):
        errors.append("source_decision_table_invalid")
    if not _exact_keys(
        payload,
        {
            "contractName",
            "contractVersion",
            "scope",
            "candidateReleaseIdentity",
            "sourceContract",
            "approvalKeyRegistry",
            "decisions",
        },
    ):
        errors.append("packet_shape_invalid")
    if not isinstance(payload, dict):
        payload = {}
    if payload.get("contractName") != PACKET_CONTRACT_NAME:
        errors.append("packet_contract_name_invalid")
    if payload.get("contractVersion") != PACKET_CONTRACT_VERSION:
        errors.append("packet_contract_version_invalid")
    if payload.get("scope") != PACKET_SCOPE:
        errors.append("packet_scope_invalid")
    candidate_release_identity = payload.get("candidateReleaseIdentity")
    if candidate_release_identity is not None and not _safe_text(
        candidate_release_identity,
        maximum=160,
    ):
        errors.append("candidate_release_identity_invalid")
    source_contract = payload.get("sourceContract")
    if not _exact_keys(source_contract, {"path", "sha256"}):
        errors.append("source_contract_shape_invalid")
        source_contract = {}
    if source_contract.get("path") != SOURCE_CONTRACT_PATH:
        errors.append("source_contract_path_invalid")
    if source_contract.get("sha256") != source_sha256:
        errors.append("source_contract_digest_mismatch")
    approval_key_registry = payload.get("approvalKeyRegistry")
    if not _exact_keys(approval_key_registry, {"path", "sha256"}):
        errors.append("approval_key_registry_ref_shape_invalid")
        approval_key_registry = {}
    if approval_key_registry.get("path") != APPROVAL_KEY_REGISTRY_PATH:
        errors.append("approval_key_registry_path_invalid")
    if approval_key_registry.get("sha256") != approval_registry_sha256:
        errors.append("approval_key_registry_digest_mismatch")

    decisions = payload.get("decisions")
    if not isinstance(decisions, list):
        errors.append("decisions_must_be_array")
        decisions = []
    actual_ids = [row.get("id") if isinstance(row, dict) else None for row in decisions]
    expected_ids = [spec.decision_id for spec in DECISION_SPECS]
    if actual_ids != expected_ids:
        errors.append("decision_order_or_identity_invalid")
    if any(not isinstance(decision_id, str) for decision_id in actual_ids):
        errors.append("decision_ids_must_be_strings")
    elif len(set(actual_ids)) != len(actual_ids):
        errors.append("decision_ids_not_unique")

    for index, spec in enumerate(DECISION_SPECS):
        if index >= len(decisions) or not isinstance(decisions[index], dict):
            invalid_ids.add(spec.decision_id)
            continue
        decision = decisions[index]
        start_error_count = len(errors)
        expected_keys = {
            "id",
            "title",
            "requiredOwnerRoles",
            "requiredAnswerFacets",
            "requiredAnswerSchema",
            "requiredEvidenceKinds",
            "requiredEvidenceProofArtifacts",
            "resolution",
        }
        if not _exact_keys(decision, expected_keys):
            errors.append(f"{spec.decision_id}:shape_invalid")
        if decision.get("id") != spec.decision_id:
            errors.append(f"{spec.decision_id}:id_invalid")
        if decision.get("title") != spec.title:
            errors.append(f"{spec.decision_id}:title_invalid")
        if decision.get("requiredOwnerRoles") != list(spec.owner_roles):
            errors.append(f"{spec.decision_id}:owner_roles_invalid")
        if decision.get("requiredAnswerFacets") != list(spec.answer_facets):
            errors.append(f"{spec.decision_id}:answer_contract_invalid")
        expected_answer_schema = {
            facet: FACET_VALUE_KINDS[(spec.decision_id, facet)]
            for facet in spec.answer_facets
        }
        if decision.get("requiredAnswerSchema") != expected_answer_schema:
            errors.append(f"{spec.decision_id}:answer_schema_contract_invalid")
        if decision.get("requiredEvidenceKinds") != list(spec.evidence_kinds):
            errors.append(f"{spec.decision_id}:evidence_contract_invalid")
        expected_evidence_proof_artifacts = {
            evidence_kind: list(EVIDENCE_PROOF_ARTIFACT_TYPES[evidence_kind])
            for evidence_kind in spec.evidence_kinds
        }
        if (
            decision.get("requiredEvidenceProofArtifacts")
            != expected_evidence_proof_artifacts
        ):
            errors.append(
                f"{spec.decision_id}:evidence_proof_artifact_contract_invalid"
            )
        resolution = decision.get("resolution")
        resolution_keys = {
            "decisionStatus",
            "accountableOwner",
            "answers",
            "resolutionRationale",
            "approvals",
            "evidenceRefs",
        }
        if not _exact_keys(resolution, resolution_keys):
            errors.append(f"{spec.decision_id}:resolution_shape_invalid")
            resolution = {}
        decision_status = resolution.get("decisionStatus")
        if decision_status == "unresolved":
            errors.extend(_validate_unresolved(spec.decision_id, resolution))
            unresolved_ids.append(spec.decision_id)
        elif decision_status == "approved":
            errors.extend(
                _validate_approved(
                    spec,
                    resolution,
                    source_sha256,
                    workspace_root,
                    observed_at,
                    candidate_release_identity,
                    trusted_approval_keys,
                    approval_registry_status,
                )
            )
            approved_ids.append(spec.decision_id)
        else:
            errors.append(f"{spec.decision_id}:decision_status_invalid")
        if len(errors) != start_error_count:
            invalid_ids.add(spec.decision_id)

    cross_policy_errors, cross_policy_invalid_ids = _cross_decision_policy_errors(decisions)
    errors.extend(cross_policy_errors)
    invalid_ids.update(cross_policy_invalid_ids)

    errors = list(dict.fromkeys(errors))
    invalid = bool(errors)
    review_required = invalid or bool(unresolved_ids)
    decision_gate_passed = not review_required and len(approved_ids) == len(DECISION_SPECS)
    if invalid:
        status = "invalid"
        reason = "Hosted Build V002 operator decision packet is malformed, stale, or evidence-unbound."
        blockers = ["hosted_build_v002_operator_decision_packet_invalid"]
    elif unresolved_ids:
        status = "review_required"
        reason = (
            "Hosted Build V002 operator decisions remain unresolved: "
            + ", ".join(unresolved_ids)
            + "."
        )
        blockers = ["hosted_build_v002_operator_decisions_unresolved"]
    else:
        status = "pass"
        reason = (
            "Hosted Build V002 operator decisions are explicit and evidence-bound; "
            "migration, production, privacy, recovery, and exact-release gates remain separate."
        )
        blockers = []
    return {
        "contractName": RECEIPT_CONTRACT_NAME,
        "contractVersion": RECEIPT_CONTRACT_VERSION,
        "generatedAtUtc": generated_at_utc,
        "status": status,
        "reviewRequired": review_required,
        "decisionGatePassed": decision_gate_passed,
        "canonicalProvenance": canonical_provenance,
        "scope": PACKET_SCOPE,
        "candidateReleaseIdentity": candidate_release_identity,
        "sourceContract": {
            "path": SOURCE_CONTRACT_PATH,
            "sha256": source_sha256,
        },
        "approvalKeyRegistry": {
            "path": (
                APPROVAL_KEY_REGISTRY_PATH
                if canonical_provenance
                else "noncanonical-input"
            ),
            "sha256": approval_registry_sha256,
            "status": approval_registry_status,
            "activeKeyCount": len(trusted_approval_keys),
        },
        "packet": {
            "path": (
                ".codex-design/product/HOSTED_BUILD_V002_OPERATOR_DECISIONS.json"
                if canonical_provenance
                else "noncanonical-input"
            ),
            "sha256": digest_bytes(packet_bytes),
        },
        "decisionCount": len(DECISION_SPECS),
        "approvedDecisionIds": approved_ids,
        "unresolvedDecisionIds": unresolved_ids,
        "invalidDecisionIds": sorted(invalid_ids),
        "blockedClaims": BLOCKED_CLAIMS if review_required else [],
        "doesNotAuthorize": DOES_NOT_AUTHORIZE,
        "blockers": blockers,
        "validationErrors": errors,
        "reason": reason,
    }


def _read_path_file(path: Path, maximum_bytes: int) -> bytes:
    absolute = Path(os.path.abspath(path))
    parts = absolute.parts[1:]
    if not parts:
        raise ValueError("file_unavailable_or_unsafe")
    flags = os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0)
    directory_flags = flags | getattr(os, "O_DIRECTORY", 0)
    descriptors: list[int] = []
    try:
        current = os.open(absolute.anchor, directory_flags)
        descriptors.append(current)
        for component in parts[:-1]:
            current = os.open(component, directory_flags, dir_fd=current)
            descriptors.append(current)
        descriptor = os.open(parts[-1], flags, dir_fd=current)
        descriptors.append(descriptor)
    except OSError as error:
        for open_descriptor in reversed(descriptors):
            os.close(open_descriptor)
        raise ValueError("file_unavailable_or_unsafe") from error
    try:
        file_stat = os.fstat(descriptor)
        if not stat.S_ISREG(file_stat.st_mode) or file_stat.st_size > maximum_bytes:
            raise ValueError("file_not_bounded_regular")
        chunks: list[bytes] = []
        remaining = maximum_bytes + 1
        while remaining > 0:
            chunk = os.read(descriptor, min(65536, remaining))
            if not chunk:
                break
            chunks.append(chunk)
            remaining -= len(chunk)
        value = b"".join(chunks)
    finally:
        for open_descriptor in reversed(descriptors):
            try:
                os.close(open_descriptor)
            except OSError:
                pass
    if len(value) > maximum_bytes:
        raise ValueError("file_too_large")
    return value


def _load_packet(path: Path) -> tuple[Any, bytes]:
    try:
        packet_bytes = _read_path_file(path, MAX_PACKET_BYTES)
    except ValueError as error:
        mapping = {
            "file_unavailable_or_unsafe": "packet_unavailable_or_unsafe",
            "file_not_bounded_regular": "packet_not_bounded_regular",
            "file_too_large": "packet_too_large",
        }
        raise ValueError(mapping.get(str(error), "packet_unavailable_or_unsafe")) from error
    try:
        return _strict_json_loads(packet_bytes), packet_bytes
    except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as error:
        raise ValueError("packet_json_invalid") from error


def _write_json_file_secure(path: Path, payload: dict[str, Any]) -> None:
    absolute = Path(os.path.abspath(path))
    parent_parts = absolute.parent.parts[1:]
    flags = os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0)
    directory_flags = flags | getattr(os, "O_DIRECTORY", 0)
    descriptors: list[int] = []
    temporary_name: str | None = None
    output_directory_descriptor: int | None = None
    try:
        current = os.open(absolute.anchor, directory_flags)
        descriptors.append(current)
        for component in parent_parts:
            current = os.open(component, directory_flags, dir_fd=current)
            descriptors.append(current)
        output_directory_descriptor = current
        try:
            existing = os.stat(absolute.name, dir_fd=current, follow_symlinks=False)
        except FileNotFoundError:
            existing = None
        if existing is not None and not stat.S_ISREG(existing.st_mode):
            raise ValueError("summary_output_not_regular")
        rendered = json.dumps(payload, ensure_ascii=False, indent=2).encode("utf-8") + b"\n"
        temporary_name = f".{absolute.name}.tmp-{os.getpid()}-{secrets.token_hex(8)}"
        write_flags = (
            os.O_WRONLY
            | os.O_CREAT
            | os.O_EXCL
            | getattr(os, "O_CLOEXEC", 0)
            | getattr(os, "O_NOFOLLOW", 0)
        )
        temporary_descriptor = os.open(
            temporary_name,
            write_flags,
            0o644,
            dir_fd=current,
        )
        descriptors.append(temporary_descriptor)
        written = 0
        while written < len(rendered):
            count = os.write(temporary_descriptor, rendered[written:])
            if count <= 0:
                raise ValueError("summary_output_write_failed")
            written += count
        os.fsync(temporary_descriptor)
        os.close(temporary_descriptor)
        descriptors.pop()
        os.replace(
            temporary_name,
            absolute.name,
            src_dir_fd=current,
            dst_dir_fd=current,
        )
        temporary_name = None
        os.fsync(current)
    except OSError as error:
        raise ValueError("summary_output_unavailable_or_unsafe") from error
    finally:
        if temporary_name is not None and output_directory_descriptor is not None:
            try:
                os.unlink(temporary_name, dir_fd=output_directory_descriptor)
            except OSError:
                pass
        for descriptor in reversed(descriptors):
            try:
                os.close(descriptor)
            except OSError:
                pass


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate Hosted Build V002 operator decisions without applying a migration."
    )
    parser.add_argument(
        "--packet",
        type=Path,
        default=DEFAULT_PACKET,
        help="diagnostic input override; noncanonical input is always invalid and cannot pass",
    )
    parser.add_argument(
        "--workspace-root",
        type=Path,
        default=WORKSPACE_ROOT,
        help="diagnostic workspace override; noncanonical input is always invalid and cannot pass",
    )
    parser.add_argument("--summary-output", type=Path, default=DEFAULT_SUMMARY)
    parser.add_argument(
        "--generated-at-utc",
        default=None,
        help="deterministic UTC receipt time; values over five minutes in the future fail closed",
    )
    return parser.parse_args()


def _invalid_receipt(
    generated_at_utc: str,
    code: str,
    *,
    canonical_provenance: bool,
) -> dict[str, Any]:
    return {
        "contractName": RECEIPT_CONTRACT_NAME,
        "contractVersion": RECEIPT_CONTRACT_VERSION,
        "generatedAtUtc": generated_at_utc,
        "status": "invalid",
        "reviewRequired": True,
        "decisionGatePassed": False,
        "canonicalProvenance": canonical_provenance,
        "scope": PACKET_SCOPE,
        "candidateReleaseIdentity": None,
        "sourceContract": {"path": SOURCE_CONTRACT_PATH, "sha256": None},
        "approvalKeyRegistry": {
            "path": (
                APPROVAL_KEY_REGISTRY_PATH
                if canonical_provenance
                else "noncanonical-input"
            ),
            "sha256": None,
            "status": None,
            "activeKeyCount": 0,
        },
        "packet": {
            "path": (
                ".codex-design/product/HOSTED_BUILD_V002_OPERATOR_DECISIONS.json"
                if canonical_provenance
                else "noncanonical-input"
            ),
            "sha256": None,
        },
        "decisionCount": len(DECISION_SPECS),
        "approvedDecisionIds": [],
        "unresolvedDecisionIds": [],
        "invalidDecisionIds": [spec.decision_id for spec in DECISION_SPECS],
        "blockedClaims": BLOCKED_CLAIMS,
        "doesNotAuthorize": DOES_NOT_AUTHORIZE,
        "blockers": ["hosted_build_v002_operator_decision_packet_invalid"],
        "validationErrors": [code],
        "reason": "Hosted Build V002 operator decision packet is unavailable or invalid.",
    }


def main() -> int:
    args = parse_args()
    generated_at_utc = args.generated_at_utc or _now_iso()
    canonical_provenance = (
        Path(os.path.abspath(args.packet)) == Path(os.path.abspath(DEFAULT_PACKET))
        and Path(os.path.abspath(args.workspace_root))
        == Path(os.path.abspath(WORKSPACE_ROOT))
    )
    try:
        parsed_generated_at = _parse_utc(generated_at_utc)
        if parsed_generated_at is None:
            raise ValueError("generated_at_utc_invalid")
        if parsed_generated_at > datetime.now(UTC) + timedelta(minutes=5):
            raise ValueError("generated_at_utc_future")
        if datetime.now(UTC) - parsed_generated_at > MAX_RECEIPT_AGE:
            raise ValueError("generated_at_utc_stale")
        if (
            Path(os.path.abspath(args.summary_output))
            == Path(os.path.abspath(DEFAULT_SUMMARY))
            and not canonical_provenance
        ):
            raise ValueError("noncanonical_inputs_cannot_write_canonical_summary")
        payload, packet_bytes = _load_packet(args.packet)
        source_bytes = _read_repo_file(
            args.workspace_root,
            "chummer-presentation",
            SOURCE_CONTRACT_PATH,
            MAX_PACKET_BYTES,
        )
        approval_registry_bytes = _read_repo_file(
            args.workspace_root,
            "chummer-presentation",
            APPROVAL_KEY_REGISTRY_PATH,
            MAX_PACKET_BYTES,
        )
        approval_registry_payload = _strict_json_loads(approval_registry_bytes)
        receipt = evaluate_packet(
            payload,
            packet_bytes=packet_bytes,
            source_bytes=source_bytes,
            approval_registry_payload=approval_registry_payload,
            approval_registry_bytes=approval_registry_bytes,
            workspace_root=args.workspace_root,
            generated_at_utc=generated_at_utc,
            canonical_provenance=canonical_provenance,
        )
    except Exception as error:
        code = str(error) if str(error) in {
            "generated_at_utc_invalid",
            "generated_at_utc_future",
            "generated_at_utc_stale",
            "packet_unavailable_or_unsafe",
            "packet_not_bounded_regular",
            "packet_too_large",
            "packet_json_invalid",
            "noncanonical_inputs_cannot_write_canonical_summary",
            "file_unavailable_or_unsafe",
            "file_not_bounded_regular",
            "file_too_large",
        } else "decision_gate_input_invalid"
        receipt = _invalid_receipt(
            generated_at_utc,
            code,
            canonical_provenance=canonical_provenance,
        )

    try:
        _write_json_file_secure(args.summary_output, receipt)
    except ValueError:
        print("hosted_build_v002_operator_decisions:summary_write_failed", file=sys.stderr)
        return 2
    if receipt["status"] == "pass":
        print("hosted_build_v002_operator_decisions:pass")
        return 0
    if receipt["status"] == "review_required":
        print("hosted_build_v002_operator_decisions:review_required", file=sys.stderr)
        return 1
    print("hosted_build_v002_operator_decisions:invalid", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
