from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
from pathlib import Path
from types import ModuleType

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
LOCK = REPO_ROOT / "config" / "package-plane.lock.json"


def load_module() -> ModuleType:
    spec = importlib.util.spec_from_file_location("fresh_package_plane_current", SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


package_plane = load_module()


def canonical_digest(value: object) -> str:
    encoded = json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def test_current_owner_contract_feed_is_separate_and_reproducible() -> None:
    lock = package_plane.load_json(LOCK)
    package_plane.validate_lock(lock)
    assert lock["contractVersion"] == 11

    current = lock["currentOwnerContractFeed"]
    canonical = lock["canonicalOwnerFeed"]
    assert current["producerCommit"] == "ed36cd2d2fab3344a44dbddc2f4b1866d362877d"
    assert current["lockContract"] == "chummer-core.package-plane-lock/v1"
    assert current["lockSha256"] == "ac731fe6e4ce7f9f2b7173fcec600769f0f76566734dc962f0ed61f68527e1fd"
    assert current["packageVersion"] == "0.0.0-packageplane.20260721.1"
    assert current["inventoryContract"] == "chummer-core.owner-contract-package-inventory/v1"
    assert current["inventorySha256"] == "81c92d4c8ce94a302fd094f6eb666bf32e338e5047b9c91b8fb37058192ab4d0"
    assert current["inventorySha256"] != current["packageFeedInventorySha256"]
    assert current["producerSha256"] == "5beffe15d1708f4cf2c096f376822cd48b9502b5fb1acb69b95db3c86c474786"

    feed_rows = sorted(
        (
            {
                "fileName": row["fileName"],
                "sha256": row["sha256"],
                "sizeBytes": row["sizeBytes"],
            }
            for row in current["packages"]
        ),
        key=lambda row: row["fileName"],
    )
    assert canonical_digest(feed_rows) == current["packageFeedInventorySha256"]
    assert current["packageFeedInventorySha256"] == "ad220c6384644fcd83135e70bb33913e546c758eedfa2fd6da514714730285ca"

    assert canonical["producerCommit"] == "bc199cbe0982833ec2fc9ce625826e612759d67a"
    assert canonical["lockContract"] == "chummer-hub.package-plane-lock/v5"
    assert canonical["inventoryContract"] == "chummer-hub.external-package-inventory/v4"
    assert len(canonical["packages"]) == 4
    assert canonical["packageVersion"] == "0.1.0-packageplane.candidate.sh1852ea4eef6d"
    assert current["lockContract"] != canonical["lockContract"]
    assert current["inventoryContract"] != canonical["inventoryContract"]
    assert current["packageVersion"] != canonical["packageVersion"]


@pytest.mark.parametrize(
    ("field", "value"),
    (
        ("producerCommit", "f" * 40),
        ("producerSha256", "f" * 64),
        ("inventorySha256", "f" * 64),
        ("packageFeedInventorySha256", "f" * 64),
    ),
)
def test_substituted_current_owner_contract_authority_is_rejected(
    field: str, value: str
) -> None:
    lock = package_plane.load_json(LOCK)
    forged = copy.deepcopy(lock)
    forged["currentOwnerContractFeed"][field] = value
    with pytest.raises(
        package_plane.VerificationError,
        match="current owner-contract package authority",
    ):
        package_plane.validate_lock(forged)


def test_current_feed_validation_is_distinct_from_full_feed_import() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    assert "def validate_materialized_current_owner_contract_feed(" in source
    assert "current_owner_contract_feed_binding_receipt(lock)" in source
    assert 'parser.add_argument("--current-owner-contract-feed", type=Path)' in source
    assert '"selectedForCanonicalFullFeed": False' in source
    assert '"currentOwnerContractFeed": (' in source
    assert "destination_feed" not in source.split(
        "def validate_materialized_current_owner_contract_feed(", 1
    )[1].split("def import_current_owner_contract_feed(", 1)[0]

    lock = package_plane.load_json(LOCK)
    receipt = package_plane.current_owner_contract_feed_binding_receipt(lock)
    assert receipt["status"] == "bound_not_selected"
    assert receipt["materializedFeedValidated"] is False
    assert receipt["selectedForCanonicalFullFeed"] is False
