from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
PLANNER_PATH = REPO_ROOT / "scripts" / "plan-hosted-build-owner-v2-migration.py"
SPEC = importlib.util.spec_from_file_location("hosted_build_owner_v2_planner", PLANNER_PATH)
assert SPEC is not None and SPEC.loader is not None
PLANNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PLANNER)


def digest(byte: str) -> str:
    return "sha256:" + (byte * 64)


def mapping(legacy: str, issuer: str, subject: str, evidence: str) -> dict[str, str]:
    return {
        "legacyOwner": legacy,
        "issuer": issuer,
        "subject": subject,
        "evidenceRef": evidence,
    }


def record(record_id: str, legacy: str, digest_nibble: str) -> dict[str, str]:
    return {
        "recordId": record_id,
        "legacyOwner": legacy,
        "contentDigest": digest(digest_nibble),
    }


def payloads(
    mappings: list[dict[str, str]],
    records: list[dict[str, str]],
) -> tuple[dict[str, object], dict[str, object]]:
    return (
        {
            "schemaVersion": 1,
            "expectedMappingCount": len(mappings),
            "mappings": mappings,
        },
        {
            "schemaVersion": 1,
            "expectedRecordCount": len(records),
            "records": records,
        },
    )


def test_exact_framing_matches_runtime_golden_vector() -> None:
    assert (
        PLANNER.derive_v2_owner(
            "https://identity.chummer.test",
            "Alice@example.com",
        )
        == PLANNER.GOLDEN_OWNER
        == "authenticated-v2-777a4e91a40ee433fc820d1fe529caf4c39b2f702ab566dac7517dbe739ae406"
    )


def test_approved_plan_is_stable_across_input_order_and_keeps_exact_variants_distinct() -> None:
    mappings = [
        mapping("legacy-a", "https://id.one.test", "Alice", "provider:a"),
        mapping("legacy-b", "https://id.one.test", "alice", "provider:b"),
        mapping("legacy-c", "https://id.one.test", "Caf\u00e9", "provider:c"),
        mapping("legacy-d", "https://id.one.test", "Cafe\u0301", "provider:d"),
        mapping("legacy-e", "https://id.two.test", "Alice", "provider:e"),
    ]
    records = [
        record("record-e", "legacy-e", "e"),
        record("record-a", "legacy-a", "a"),
        record("record-d", "legacy-d", "d"),
        record("record-b", "legacy-b", "b"),
        record("record-c", "legacy-c", "c"),
    ]
    forward = PLANNER.build_plan(*payloads(mappings, records))
    reverse = PLANNER.build_plan(*payloads(list(reversed(mappings)), list(reversed(records))))

    assert forward["status"] == "approved"
    assert PLANNER.render_plan(forward) == PLANNER.render_plan(reverse)
    approved = forward["approvedManifest"]["mappings"]
    assert [row["legacyOwner"] for row in approved] == [
        "legacy-a",
        "legacy-b",
        "legacy-c",
        "legacy-d",
        "legacy-e",
    ]
    assert len({row["v2Owner"] for row in approved}) == 5


def test_one_legacy_owner_with_multiple_exact_identities_is_quarantined() -> None:
    inputs = payloads(
        [
            mapping("legacy-a", "https://id.test", "Alice", "provider:a"),
            mapping("legacy-a", "https://id.test", "alice", "provider:b"),
        ],
        [record("record-a", "legacy-a", "a")],
    )
    plan = PLANNER.build_plan(*inputs)

    assert plan["status"] == "blocked"
    assert plan["approvedManifest"]["mappings"] == []
    bucket = plan["quarantinedManifest"]["buckets"][0]
    assert bucket["reasons"] == ["legacy_owner_maps_to_multiple_exact_identities"]
    assert bucket["recordCount"] == 1


def test_multiple_legacy_owners_converging_on_one_v2_owner_are_quarantined() -> None:
    inputs = payloads(
        [
            mapping("legacy-a", "https://id.test", "Alice", "provider:a"),
            mapping("legacy-b", "https://id.test", "Alice", "provider:b"),
        ],
        [
            record("record-a", "legacy-a", "a"),
            record("record-b", "legacy-b", "b"),
        ],
    )
    plan = PLANNER.build_plan(*inputs)

    assert plan["status"] == "blocked"
    assert plan["approvedManifest"]["mappings"] == []
    assert [bucket["legacyOwner"] for bucket in plan["quarantinedManifest"]["buckets"]] == [
        "legacy-a",
        "legacy-b",
    ]
    assert all(
        bucket["reasons"] == ["multiple_legacy_owners_map_to_one_v2_owner"]
        for bucket in plan["quarantinedManifest"]["buckets"]
    )


def test_missing_mapping_is_quarantined_and_cli_returns_nonzero(tmp_path: Path) -> None:
    mappings, inventory = payloads(
        [],
        [record("orphan-record", "missing-owner", "f")],
    )
    mapping_path = tmp_path / "mapping.json"
    inventory_path = tmp_path / "inventory.json"
    mapping_path.write_text(json.dumps(mappings), encoding="utf-8")
    inventory_path.write_text(json.dumps(inventory), encoding="utf-8")

    completed = subprocess.run(
        [
            sys.executable,
            str(PLANNER_PATH),
            "--mapping",
            str(mapping_path),
            "--inventory",
            str(inventory_path),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 1
    assert completed.stderr == ""
    plan = json.loads(completed.stdout)
    assert plan["status"] == "blocked"
    assert plan["quarantinedManifest"]["buckets"][0]["reasons"] == [
        "missing_provider_mapping"
    ]


def test_invalid_input_returns_exit_two_without_a_plan(tmp_path: Path) -> None:
    mapping_path = tmp_path / "mapping.json"
    inventory_path = tmp_path / "inventory.json"
    mapping_path.write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "expectedMappingCount": 1,
                "mappings": [
                    mapping(
                        "legacy-a",
                        "https://id.test",
                        " Alice",
                        "provider:a",
                    )
                ],
            }
        ),
        encoding="utf-8",
    )
    inventory_path.write_text(
        json.dumps({"schemaVersion": 1, "expectedRecordCount": 0, "records": []}),
        encoding="utf-8",
    )

    completed = subprocess.run(
        [
            sys.executable,
            str(PLANNER_PATH),
            "--mapping",
            str(mapping_path),
            "--inventory",
            str(inventory_path),
        ],
        check=False,
        capture_output=True,
        text=True,
    )

    assert completed.returncode == 2
    assert completed.stdout == ""
    assert json.loads(completed.stderr)["status"] == "invalid"


def test_export_count_mismatch_is_invalid_instead_of_approving_a_partial_inventory() -> None:
    mappings, inventory = payloads(
        [mapping("legacy-a", "https://id.test", "Alice", "provider:a")],
        [record("record-a", "legacy-a", "a")],
    )
    inventory["expectedRecordCount"] = 2

    with pytest.raises(PLANNER.InputError, match="does not match"):
        PLANNER.build_plan(mappings, inventory)
