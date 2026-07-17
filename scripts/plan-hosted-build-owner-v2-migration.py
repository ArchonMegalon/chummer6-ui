#!/usr/bin/env python3
"""Build a deterministic, read-only Hosted Build owner-v2 migration plan.

The planner never mutates application storage. It validates provider mappings and
an exported record inventory, then writes one JSON envelope to stdout containing
separate approved and quarantined manifests. A blocked plan is still emitted but
returns exit status 1; structurally invalid input returns exit status 2.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import sys
import unicodedata
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable


SCHEMA_VERSION = 1
PLAN_ALGORITHM = "chummer-hosted-build-owner-v2-plan-v1"
PURPOSE = "Chummer.Blazor.HostedBuildAuthenticatedOwner.v2"
OWNER_PREFIX = "authenticated-v2-"
DEFAULT_ISSUER = "LOCAL AUTHORITY"
MAX_SUBJECT_UTF16_UNITS = 512
MAX_ISSUER_UTF16_UNITS = 2048
MAX_INPUT_BYTES = 64 * 1024 * 1024
SHA256_PATTERN = re.compile(r"^sha256:[0-9a-f]{64}$")
GOLDEN_OWNER = (
    "authenticated-v2-"
    "777a4e91a40ee433fc820d1fe529caf4c39b2f702ab566dac7517dbe739ae406"
)


class InputError(ValueError):
    """The exported migration evidence is malformed or incomplete."""


def _utf16_units(value: str) -> int:
    return len(value.encode("utf-16-le", errors="surrogatepass")) // 2


def _validate_identity_component(value: Any, field: str, maximum_units: int) -> str:
    if not isinstance(value, str):
        raise InputError(f"{field} must be a string")
    if not value or _utf16_units(value) > maximum_units:
        raise InputError(f"{field} must contain 1..{maximum_units} UTF-16 code units")
    if value[0].isspace() or value[-1].isspace():
        raise InputError(f"{field} must not contain surrounding whitespace")
    if any(unicodedata.category(character) == "Cc" for character in value):
        raise InputError(f"{field} must not contain control characters")
    try:
        value.encode("utf-8", errors="strict")
    except UnicodeEncodeError as error:
        raise InputError(f"{field} must be valid strict UTF-8") from error
    return value


def derive_v2_owner(issuer: str, subject: str) -> str:
    """Apply the runtime's exact purpose/BE32/strict-UTF-8 framing."""

    issuer = _validate_identity_component(
        issuer, "issuer", MAX_ISSUER_UTF16_UNITS
    )
    subject = _validate_identity_component(
        subject, "subject", MAX_SUBJECT_UTF16_UNITS
    )
    if issuer == DEFAULT_ISSUER:
        raise InputError("issuer must be provider-qualified, not LOCAL AUTHORITY")
    purpose_bytes = PURPOSE.encode("utf-8")
    issuer_bytes = issuer.encode("utf-8")
    subject_bytes = subject.encode("utf-8")
    framed = b"".join(
        (
            purpose_bytes,
            b"\x00",
            struct.pack(">I", len(issuer_bytes)),
            issuer_bytes,
            struct.pack(">I", len(subject_bytes)),
            subject_bytes,
        )
    )
    return OWNER_PREFIX + hashlib.sha256(framed).hexdigest()


def _exact_text(value: Any, field: str) -> str:
    if not isinstance(value, str) or not value:
        raise InputError(f"{field} must be a non-empty string")
    if value != value.strip():
        raise InputError(f"{field} must not contain surrounding whitespace")
    if any(unicodedata.category(character) == "Cc" for character in value):
        raise InputError(f"{field} must not contain control characters")
    try:
        value.encode("utf-8", errors="strict")
    except UnicodeEncodeError as error:
        raise InputError(f"{field} must be valid strict UTF-8") from error
    return value


def _canonical_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def _digest(value: Any) -> str:
    return "sha256:" + hashlib.sha256(_canonical_bytes(value)).hexdigest()


def _require_payload(
    payload: Any,
    collection_name: str,
    expected_count_name: str,
) -> list[dict[str, Any]]:
    if not isinstance(payload, dict):
        raise InputError("input root must be a JSON object")
    if payload.get("schemaVersion") != SCHEMA_VERSION:
        raise InputError(f"schemaVersion must equal {SCHEMA_VERSION}")
    rows = payload.get(collection_name)
    if not isinstance(rows, list):
        raise InputError(f"{collection_name} must be an array")
    if any(not isinstance(row, dict) for row in rows):
        raise InputError(f"every {collection_name} entry must be an object")
    expected_count = payload.get(expected_count_name)
    if isinstance(expected_count, bool) or not isinstance(expected_count, int):
        raise InputError(f"{expected_count_name} must be a non-negative integer")
    if expected_count < 0 or expected_count != len(rows):
        raise InputError(
            f"{expected_count_name}={expected_count} does not match {len(rows)} exported {collection_name} entries"
        )
    return rows


def _record_manifest(records: Iterable[dict[str, str]]) -> list[dict[str, str]]:
    return sorted(records, key=lambda row: row["recordId"])


def build_plan(mapping_payload: Any, inventory_payload: Any) -> dict[str, Any]:
    mapping_rows = _require_payload(
        mapping_payload,
        "mappings",
        "expectedMappingCount",
    )
    inventory_rows = _require_payload(
        inventory_payload,
        "records",
        "expectedRecordCount",
    )

    mappings_by_legacy: dict[str, list[dict[str, str]]] = defaultdict(list)
    for index, row in enumerate(mapping_rows):
        legacy_owner = _exact_text(row.get("legacyOwner"), f"mappings[{index}].legacyOwner")
        issuer = _validate_identity_component(
            row.get("issuer"),
            f"mappings[{index}].issuer",
            MAX_ISSUER_UTF16_UNITS,
        )
        subject = _validate_identity_component(
            row.get("subject"),
            f"mappings[{index}].subject",
            MAX_SUBJECT_UTF16_UNITS,
        )
        if issuer == DEFAULT_ISSUER:
            raise InputError(f"mappings[{index}].issuer must be provider-qualified")
        evidence_ref = _exact_text(
            row.get("evidenceRef"), f"mappings[{index}].evidenceRef"
        )
        mappings_by_legacy[legacy_owner].append(
            {
                "legacyOwner": legacy_owner,
                "issuer": issuer,
                "subject": subject,
                "v2Owner": derive_v2_owner(issuer, subject),
                "evidenceRef": evidence_ref,
            }
        )

    records_by_legacy: dict[str, list[dict[str, str]]] = defaultdict(list)
    seen_record_ids: set[str] = set()
    for index, row in enumerate(inventory_rows):
        record_id = _exact_text(row.get("recordId"), f"records[{index}].recordId")
        if record_id in seen_record_ids:
            raise InputError(f"duplicate recordId: {record_id}")
        seen_record_ids.add(record_id)
        legacy_owner = _exact_text(row.get("legacyOwner"), f"records[{index}].legacyOwner")
        content_digest = row.get("contentDigest")
        if not isinstance(content_digest, str) or not SHA256_PATTERN.fullmatch(content_digest):
            raise InputError(
                f"records[{index}].contentDigest must be lowercase sha256:<64 hex>"
            )
        records_by_legacy[legacy_owner].append(
            {
                "recordId": record_id,
                "legacyOwner": legacy_owner,
                "contentDigest": content_digest,
            }
        )

    exact_tuples_by_legacy: dict[str, set[tuple[str, str, str]]] = {}
    legacy_owners_by_v2: dict[str, set[str]] = defaultdict(set)
    for legacy_owner, rows in mappings_by_legacy.items():
        exact_tuples = {
            (row["issuer"], row["subject"], row["v2Owner"]) for row in rows
        }
        exact_tuples_by_legacy[legacy_owner] = exact_tuples
        for _, _, v2_owner in exact_tuples:
            legacy_owners_by_v2[v2_owner].add(legacy_owner)

    all_legacy_owners = sorted(set(mappings_by_legacy) | set(records_by_legacy))
    blocked_reasons: dict[str, set[str]] = defaultdict(set)
    for legacy_owner in all_legacy_owners:
        tuples = exact_tuples_by_legacy.get(legacy_owner, set())
        if not tuples:
            blocked_reasons[legacy_owner].add("missing_provider_mapping")
            continue
        if len(tuples) != 1:
            blocked_reasons[legacy_owner].add(
                "legacy_owner_maps_to_multiple_exact_identities"
            )
        for _, _, v2_owner in tuples:
            if len(legacy_owners_by_v2[v2_owner]) != 1:
                blocked_reasons[legacy_owner].add(
                    "multiple_legacy_owners_map_to_one_v2_owner"
                )

    approved: list[dict[str, Any]] = []
    quarantined: list[dict[str, Any]] = []
    for legacy_owner in all_legacy_owners:
        records = _record_manifest(records_by_legacy.get(legacy_owner, []))
        mapping_candidates = sorted(
            {
                (
                    row["issuer"],
                    row["subject"],
                    row["v2Owner"],
                )
                for row in mappings_by_legacy.get(legacy_owner, [])
            }
        )
        candidate_manifest = [
            {"issuer": issuer, "subject": subject, "v2Owner": v2_owner}
            for issuer, subject, v2_owner in mapping_candidates
        ]
        reasons = sorted(blocked_reasons.get(legacy_owner, set()))
        if reasons:
            quarantined.append(
                {
                    "legacyOwner": legacy_owner,
                    "reasons": reasons,
                    "candidateMappings": candidate_manifest,
                    "recordCount": len(records),
                    "recordSetDigest": _digest(records),
                    "records": records,
                }
            )
            continue

        issuer, subject, v2_owner = next(iter(exact_tuples_by_legacy[legacy_owner]))
        evidence_refs = sorted(
            {
                row["evidenceRef"]
                for row in mappings_by_legacy[legacy_owner]
                if row["issuer"] == issuer and row["subject"] == subject
            }
        )
        approved.append(
            {
                "legacyOwner": legacy_owner,
                "v2Owner": v2_owner,
                "issuer": issuer,
                "subject": subject,
                "evidenceRefs": evidence_refs,
                "recordCount": len(records),
                "recordSetDigest": _digest(records),
                "records": records,
            }
        )

    approved.sort(key=lambda row: (row["legacyOwner"], row["v2Owner"]))
    quarantined.sort(key=lambda row: row["legacyOwner"])
    status = "approved" if not quarantined else "blocked"
    approved_manifest = {
        "schemaVersion": SCHEMA_VERSION,
        "status": status,
        "mappings": approved,
    }
    quarantined_manifest = {
        "schemaVersion": SCHEMA_VERSION,
        "status": status,
        "buckets": quarantined,
    }
    normalized_mapping_source = {
        "schemaVersion": SCHEMA_VERSION,
        "expectedMappingCount": len(mapping_rows),
        "mappings": sorted(
            (
                {
                    "legacyOwner": row["legacyOwner"],
                    "issuer": row["issuer"],
                    "subject": row["subject"],
                    "evidenceRef": row["evidenceRef"],
                }
                for rows in mappings_by_legacy.values()
                for row in rows
            ),
            key=lambda row: (
                row["legacyOwner"],
                row["issuer"],
                row["subject"],
                row["evidenceRef"],
            ),
        ),
    }
    normalized_inventory_source = {
        "schemaVersion": SCHEMA_VERSION,
        "expectedRecordCount": len(inventory_rows),
        "records": sorted(
            (
                row
                for rows in records_by_legacy.values()
                for row in rows
            ),
            key=lambda row: row["recordId"],
        ),
    }
    plan = {
        "schemaVersion": SCHEMA_VERSION,
        "algorithm": PLAN_ALGORITHM,
        "status": status,
        "sourceDigests": {
            "mapping": _digest(normalized_mapping_source),
            "inventory": _digest(normalized_inventory_source),
        },
        "goldenVector": {
            "issuer": "https://identity.chummer.test",
            "subject": "Alice@example.com",
            "owner": GOLDEN_OWNER,
        },
        "summary": {
            "approvedOwnerCount": len(approved),
            "approvedRecordCount": sum(row["recordCount"] for row in approved),
            "quarantinedOwnerCount": len(quarantined),
            "quarantinedRecordCount": sum(
                row["recordCount"] for row in quarantined
            ),
        },
        "approvedManifest": approved_manifest,
        "quarantinedManifest": quarantined_manifest,
    }
    plan["planDigest"] = _digest(plan)
    return plan


def render_plan(plan: dict[str, Any]) -> str:
    return json.dumps(plan, ensure_ascii=False, sort_keys=True, indent=2) + "\n"


def _load_json(path: Path) -> Any:
    size = path.stat().st_size
    if size > MAX_INPUT_BYTES:
        raise InputError(f"{path} exceeds the {MAX_INPUT_BYTES}-byte planner limit")
    try:
        with path.open("r", encoding="utf-8", errors="strict") as stream:
            return json.load(stream)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise InputError(f"could not read strict JSON from {path}: {error}") from error


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mapping", required=True, type=Path)
    parser.add_argument("--inventory", required=True, type=Path)
    arguments = parser.parse_args(argv)
    try:
        if derive_v2_owner(
            "https://identity.chummer.test", "Alice@example.com"
        ) != GOLDEN_OWNER:
            raise InputError("internal v2 golden vector mismatch")
        plan = build_plan(
            _load_json(arguments.mapping),
            _load_json(arguments.inventory),
        )
    except InputError as error:
        print(
            json.dumps(
                {
                    "schemaVersion": SCHEMA_VERSION,
                    "status": "invalid",
                    "error": str(error),
                },
                sort_keys=True,
            ),
            file=sys.stderr,
        )
        return 2

    sys.stdout.write(render_plan(plan))
    return 0 if plan["status"] == "approved" else 1


if __name__ == "__main__":
    raise SystemExit(main())
