from __future__ import annotations

import importlib.util
import hashlib
import io
import json
import os
import shutil
import stat
import subprocess
import textwrap
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path
from types import ModuleType, SimpleNamespace

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
LOCK = REPO_ROOT / "config" / "package-plane.lock.json"


def load_module() -> ModuleType:
    spec = importlib.util.spec_from_file_location("fresh_package_plane", SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


package_plane = load_module()


def test_sealed_next_transition_derives_exact_unsealed_upstream_without_mutation() -> None:
    previous = json.loads(LOCK.read_text(encoding="utf-8"))
    previous_bytes = package_plane.encoded_json(previous)
    next_lock = package_plane.build_next_unsealed_authority_lock(
        REPO_ROOT, previous
    )

    assert package_plane.encoded_json(previous) == previous_bytes
    assert previous["contractVersion"] == 11
    assert "uiOwnerFeed" in previous
    assert next_lock["contractVersion"] == 10
    assert "uiOwnerFeed" not in next_lock
    assert next_lock["coreRuntimeFeed"]["packageRecipeCommit"] == (
        "c06f22c185c7b733637fdb76b3cf333f31716781"
    )
    assert next_lock["coreRuntimeFeed"]["runtimeSourceCommit"] == (
        "60112dccb6a3faad330d32c3c98eef0aa81d97af"
    )
    assert next_lock["canonicalOwnerFeed"]["producerCommit"] == (
        "bc199cbe0982833ec2fc9ce625826e612759d67a"
    )
    assert package_plane.UI_OWNER_PRODUCER_LOCK_PATH not in (
        next_lock["consumer"]["sourceFiles"]
    )
    assert package_plane.SEALED_NEXT_AUTHORITY_ORACLE == {
        "canonicalLock": {
            "blob": "e9a9a3d19c35384e481d0a70ed9160fc0557e369",
            "commit": "c12811fda570cd56c70e52c44e38b1d32ff831a1",
            "fixturePath": "config/ui-next-authority-oracle-v10.json",
            "path": "config/package-plane.lock.json",
            "rawSha256": "64f06037031d5d29b7904f64fb46404524f2ea1d3477851bef8cf797dece834b",
            "rawSizeBytes": 51528,
            "semanticCanonicalSha256": "69360823bfad24a3935a9a72542c761d68a71846b7448d7cc98d40c2efd926c4",
            "semanticCanonicalSizeBytes": 51528,
            "tree": "faec09b431f3f6fd94736655e4e1850bbdf5d3f2",
        },
        "producerLock": {
            "absentAtCommit": True,
            "path": "config/ui-owner-package-plane.lock.json",
        },
    }
    assert len(package_plane.encoded_json(next_lock)) == 51528
    assert hashlib.sha256(package_plane.encoded_json(next_lock)).hexdigest() == (
        "69360823bfad24a3935a9a72542c761d68a71846b7448d7cc98d40c2efd926c4"
    )
    with pytest.raises(package_plane.VerificationError):
        package_plane.validate_lock(next_lock)
    package_plane.validate_lock(next_lock, allow_unsealed_ui_owner=True)


def test_sealed_next_transition_rejects_oracle_payload_or_metadata_substitution(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    oracle = package_plane.fixed_next_authority_oracle_lock(REPO_ROOT)
    assert oracle["contractVersion"] == 10
    original_path = package_plane.SEALED_NEXT_AUTHORITY_ORACLE_PATH
    monkeypatch.setattr(
        package_plane,
        "SEALED_NEXT_AUTHORITY_ORACLE_PATH",
        "config/package-plane.lock.json",
    )
    with pytest.raises(package_plane.VerificationError, match="raw bytes differ"):
        package_plane.fixed_next_authority_oracle_lock(REPO_ROOT)
    monkeypatch.setattr(
        package_plane,
        "SEALED_NEXT_AUTHORITY_ORACLE_PATH",
        original_path,
    )
    substituted_metadata = json.loads(
        json.dumps(package_plane.SEALED_NEXT_AUTHORITY_ORACLE)
    )
    substituted_metadata["canonicalLock"]["rawSha256"] = "a" * 64
    monkeypatch.setattr(
        package_plane,
        "SEALED_NEXT_AUTHORITY_ORACLE",
        substituted_metadata,
    )
    with pytest.raises(package_plane.VerificationError, match="raw bytes differ"):
        package_plane.fixed_next_authority_oracle_lock(REPO_ROOT)


def test_sealed_next_transition_rejects_substituted_previous_lock_semantics() -> None:
    previous = json.loads(LOCK.read_text(encoding="utf-8"))
    previous["approvedPackageSources"] = ["substituted-feed"]
    with pytest.raises(package_plane.VerificationError):
        package_plane.build_next_unsealed_authority_lock(REPO_ROOT, previous)


def test_sealed_next_transition_builds_complete_proposed_two_lock_authority() -> None:
    previous = json.loads(LOCK.read_text(encoding="utf-8"))
    next_lock = package_plane.build_next_unsealed_authority_lock(
        REPO_ROOT, previous
    )
    package_rows = []
    for package_id, (owner, project, file_name, version) in (
        package_plane.EXPECTED_PACKAGES.items()
    ):
        package_rows.append(
            {
                "commit": package_plane.EXPECTED_UI_OWNER_SOURCES[package_id]["commit"],
                "fileName": file_name,
                "ownerDirectory": owner,
                "packageId": package_id,
                "project": project,
                "projectSha256": package_plane.EXPECTED_UI_OWNER_SOURCES[package_id][
                    "projectSha256"
                ],
                "repository": package_plane.EXPECTED_UI_OWNER_SOURCES[package_id][
                    "repository"
                ],
                "sha256": "a" * 64,
                "sizeBytes": 1,
                "sourceTree": package_plane.EXPECTED_UI_OWNER_SOURCES[package_id][
                    "sourceTree"
                ],
                "version": version,
            }
        )
    authority = {
        "dependencyAuthorityCacheKey": package_plane.upstream_owner_package_cache_manifest(
            next_lock
        )["cacheKey"],
        "inventoryContract": package_plane.UI_OWNER_FEED_INVENTORY_CONTRACT,
        "inventoryFileName": "ui-owner-packages.inventory.json",
        "inventorySha256": "b" * 64,
        "packageRecipeCommit": "c" * 40,
        "packageRecipeSha256": "d" * 64,
        "packages": package_rows,
        "producerLockFileName": "ui-owner-package-plane.lock.json",
        "producerLockPath": package_plane.UI_OWNER_PRODUCER_LOCK_PATH,
        "producerLockSha256": "e" * 64,
        "receiptContract": package_plane.UI_OWNER_FEED_RECEIPT_CONTRACT,
        "receiptFileName": "ui-owner-packages.receipt.json",
        "receiptSha256": "f" * 64,
        "sdkVersion": package_plane.EXPECTED_SDK_VERSION,
    }
    proposed = package_plane.build_proposed_sealed_authority_lock(
        next_lock,
        ui_owner_authority=authority,
        package_rows=package_rows,
        producer_lock_sha256="e" * 64,
    )
    assert proposed["contractVersion"] == 11
    assert proposed["uiOwnerFeed"] == authority
    assert proposed["packages"] == package_rows
    assert proposed["consumer"]["sourceFiles"][
        package_plane.UI_OWNER_PRODUCER_LOCK_PATH
    ] == "e" * 64
    package_plane.validate_lock(proposed)


def test_sealed_next_transition_is_explicit_and_cold_only(tmp_path: Path) -> None:
    with pytest.raises(package_plane.VerificationError, match="cold-input only"):
        package_plane.produce_owner_package_cache(
            SimpleNamespace(
                cold_core_runtime_bundle=None,
                cold_hub_package_plane_receipt=None,
                owner_package_cache=tmp_path,
                transition_from_sealed_preseal=True,
            )
        )


def _transition_args(tmp_path: Path) -> SimpleNamespace:
    parent = tmp_path / "transition-output"
    parent.mkdir(mode=0o700, parents=True)
    return SimpleNamespace(
        transition_from_sealed_preseal=True,
        proposed_package_plane_lock_output=parent / "package-plane.lock.json",
        proposed_ui_owner_lock_output=(
            parent / "ui-owner-package-plane.lock.json"
        ),
        produce_owner_package_cache_output=tmp_path / "owner-cache",
        receipt_output=tmp_path / "production.receipt.json",
    )


def test_sealed_next_transition_requires_exact_fresh_external_output_pair(
    tmp_path: Path,
) -> None:
    args = _transition_args(tmp_path)
    validated = package_plane.validate_transition_lock_output_targets(
        args,
        repo_root=REPO_ROOT,
        owner_cache_output=args.produce_owner_package_cache_output,
    )
    assert validated is not None
    assert validated[:2] == (
        args.proposed_package_plane_lock_output,
        args.proposed_ui_owner_lock_output,
    )

    args.proposed_ui_owner_lock_output = None
    with pytest.raises(package_plane.VerificationError, match="requires both"):
        package_plane.validate_transition_lock_output_targets(
            args,
            repo_root=REPO_ROOT,
            owner_cache_output=args.produce_owner_package_cache_output,
        )

    args = _transition_args(tmp_path / "relative")
    args.proposed_package_plane_lock_output = Path("package-plane.lock.json")
    with pytest.raises(package_plane.VerificationError, match="absolute"):
        package_plane.validate_transition_lock_output_targets(
            args,
            repo_root=REPO_ROOT,
            owner_cache_output=args.produce_owner_package_cache_output,
        )


def test_sealed_next_transition_rejects_existing_wrong_parent_and_nontransition_outputs(
    tmp_path: Path,
) -> None:
    args = _transition_args(tmp_path / "existing")
    args.proposed_package_plane_lock_output.write_text("occupied", encoding="utf-8")
    with pytest.raises(package_plane.VerificationError, match="must be absent"):
        package_plane.validate_transition_lock_output_targets(
            args,
            repo_root=REPO_ROOT,
            owner_cache_output=args.produce_owner_package_cache_output,
        )

    args = _transition_args(tmp_path / "separate")
    other = tmp_path / "other"
    other.mkdir(mode=0o700)
    args.proposed_ui_owner_lock_output = (
        other / "ui-owner-package-plane.lock.json"
    )
    with pytest.raises(package_plane.VerificationError, match="share one trusted"):
        package_plane.validate_transition_lock_output_targets(
            args,
            repo_root=REPO_ROOT,
            owner_cache_output=args.produce_owner_package_cache_output,
        )

    args = _transition_args(tmp_path / "writable")
    writable_parent = args.proposed_package_plane_lock_output.parent
    writable_parent.chmod(0o770)
    try:
        with pytest.raises(package_plane.VerificationError, match="not group/world"):
            package_plane.validate_transition_lock_output_targets(
                args,
                repo_root=REPO_ROOT,
                owner_cache_output=args.produce_owner_package_cache_output,
            )
    finally:
        writable_parent.chmod(0o700)

    args = _transition_args(tmp_path / "nontransition")
    args.transition_from_sealed_preseal = False
    with pytest.raises(package_plane.VerificationError, match="require sealed-next"):
        package_plane.validate_transition_lock_output_targets(
            args,
            repo_root=REPO_ROOT,
            owner_cache_output=args.produce_owner_package_cache_output,
        )


def test_sealed_next_transition_rejects_in_repo_receipt_without_mutation(
    tmp_path: Path,
) -> None:
    args = _transition_args(tmp_path)
    in_repo_receipt = REPO_ROOT / "transition-receipt-must-not-exist.json"
    assert not in_repo_receipt.exists()
    args.receipt_output = in_repo_receipt
    before_status = subprocess.run(
        ["git", "status", "--porcelain"],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    ).stdout
    with pytest.raises(package_plane.VerificationError, match="outside"):
        package_plane.validate_transition_lock_output_targets(
            args,
            repo_root=REPO_ROOT,
            owner_cache_output=args.produce_owner_package_cache_output,
        )
    assert not in_repo_receipt.exists()
    assert subprocess.run(
        ["git", "status", "--porcelain"],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    ).stdout == before_status


def test_transition_capture_rejects_symlinked_previous_canonical_lock(
    tmp_path: Path,
) -> None:
    repository = tmp_path / "consumer"
    (repository / "config").mkdir(parents=True)
    subprocess.run(["git", "init", "--quiet"], cwd=repository, check=True)
    subprocess.run(
        ["git", "config", "user.email", "tests@example.invalid"],
        cwd=repository,
        check=True,
    )
    subprocess.run(
        ["git", "config", "user.name", "Tests"], cwd=repository, check=True
    )
    target = repository / "actual-lock.json"
    target.write_text("{}\n", encoding="utf-8")
    canonical = repository / "config" / "package-plane.lock.json"
    canonical.symlink_to(target)
    subprocess.run(["git", "add", "--all"], cwd=repository, check=True)
    subprocess.run(
        ["git", "commit", "--quiet", "-m", "symlink lock"],
        cwd=repository,
        check=True,
    )
    with pytest.raises(package_plane.VerificationError, match="canonical"):
        package_plane.capture_consumer_authority(repository, canonical)


def test_sealed_next_transition_retains_exact_two_outputs_and_rolls_them_back(
    tmp_path: Path,
) -> None:
    args = _transition_args(tmp_path)
    validated = package_plane.validate_transition_lock_output_targets(
        args,
        repo_root=REPO_ROOT,
        owner_cache_output=args.produce_owner_package_cache_output,
    )
    assert validated is not None
    canonical_bytes = b'{"canonical":true}\n'
    producer_bytes = b'{"producer":true}\n'
    rows = package_plane.retain_transition_lock_outputs(
        args,
        validated_targets=validated,
        canonical_bytes=canonical_bytes,
        producer_bytes=producer_bytes,
    )
    assert [row["sha256"] for row in rows] == [
        hashlib.sha256(canonical_bytes).hexdigest(),
        hashlib.sha256(producer_bytes).hexdigest(),
    ]
    assert args.proposed_package_plane_lock_output.read_bytes() == canonical_bytes
    assert args.proposed_ui_owner_lock_output.read_bytes() == producer_bytes
    package_plane.rollback_pending_verification(args)
    assert not args.proposed_package_plane_lock_output.exists()
    assert not args.proposed_ui_owner_lock_output.exists()


def _prepare_retained_transition(
    tmp_path: Path,
) -> tuple[SimpleNamespace, dict[str, object]]:
    args = _transition_args(tmp_path)
    validated = package_plane.validate_transition_lock_output_targets(
        args,
        repo_root=REPO_ROOT,
        owner_cache_output=args.produce_owner_package_cache_output,
    )
    assert validated is not None
    rows = package_plane.retain_transition_lock_outputs(
        args,
        validated_targets=validated,
        canonical_bytes=b"canonical\n",
        producer_bytes=b"producer\n",
    )
    cache = args.produce_owner_package_cache_output
    cache.mkdir(mode=0o700)
    package_plane.exact_write_receipt(
        cache / "owner-package-cache.json",
        {"cacheKey": "test-cache-key"},
    )
    cache_metadata = cache.lstat()
    args._produced_owner_cache_identity = (
        cache_metadata.st_dev,
        cache_metadata.st_ino,
    )
    args._produced_owner_cache_inventory = package_plane.directory_asset_inventory(
        cache
    )
    receipt: dict[str, object] = {
        "cacheKey": "test-cache-key",
        "proposedCanonicalLockSha256": rows[0]["sha256"],
        "proposedProducerLockSha256": rows[1]["sha256"],
        "sealedNextAuthorityTransition": {"proposedLockOutputs": rows},
        "status": "passed",
        "targetPath": str(cache),
    }
    return args, receipt


@pytest.mark.parametrize(
    "output_attribute",
    ("proposed_package_plane_lock_output", "proposed_ui_owner_lock_output"),
)
def test_sealed_next_transition_rejects_same_inode_overwrite_during_receipt(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    output_attribute: str,
) -> None:
    args, receipt = _prepare_retained_transition(tmp_path)
    original_write = package_plane.exact_write_receipt
    target = getattr(args, output_attribute)
    expected_size = len(target.read_bytes())

    def overwrite_then_write(path: Path, payload: dict[str, object]) -> tuple[int, int]:
        target.write_bytes(b"x" * expected_size)
        return original_write(path, payload)

    monkeypatch.setattr(package_plane, "exact_write_receipt", overwrite_then_write)
    with pytest.raises(package_plane.VerificationError, match="receipt|rollback"):
        package_plane.commit_verification_receipt(args, receipt)
    assert not args.receipt_output.exists()
    assert not args.proposed_package_plane_lock_output.exists()
    assert not args.proposed_ui_owner_lock_output.exists()
    assert not args.produce_owner_package_cache_output.exists()


@pytest.mark.parametrize(
    "output_attribute",
    ("proposed_package_plane_lock_output", "proposed_ui_owner_lock_output"),
)
def test_sealed_next_transition_rejects_atomic_output_replacement_during_receipt(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    output_attribute: str,
) -> None:
    args, receipt = _prepare_retained_transition(tmp_path)
    original_write = package_plane.exact_write_receipt
    attacker_bytes = b"attacker\n"
    target = getattr(args, output_attribute)

    def replace_then_write(path: Path, payload: dict[str, object]) -> tuple[int, int]:
        target.unlink()
        target.write_bytes(attacker_bytes)
        return original_write(path, payload)

    monkeypatch.setattr(package_plane, "exact_write_receipt", replace_then_write)
    with pytest.raises(package_plane.VerificationError, match="rollback"):
        package_plane.commit_verification_receipt(args, receipt)
    assert not args.receipt_output.exists()
    assert target.read_bytes() == attacker_bytes
    other = (
        args.proposed_ui_owner_lock_output
        if output_attribute == "proposed_package_plane_lock_output"
        else args.proposed_package_plane_lock_output
    )
    assert not other.exists()
    assert not args.produce_owner_package_cache_output.exists()


def test_sealed_next_transition_rejects_cache_manifest_change_during_receipt(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    args, receipt = _prepare_retained_transition(tmp_path)
    original_write = package_plane.exact_write_receipt
    manifest = args.produce_owner_package_cache_output / "owner-package-cache.json"
    original_bytes = manifest.read_bytes()

    def overwrite_then_write(path: Path, payload: dict[str, object]) -> tuple[int, int]:
        manifest.write_bytes(b"x" * len(original_bytes))
        return original_write(path, payload)

    monkeypatch.setattr(package_plane, "exact_write_receipt", overwrite_then_write)
    with pytest.raises(package_plane.VerificationError, match="receipt|cache"):
        package_plane.commit_verification_receipt(args, receipt)
    assert not args.receipt_output.exists()
    assert not args.proposed_package_plane_lock_output.exists()
    assert not args.proposed_ui_owner_lock_output.exists()
    assert not args.produce_owner_package_cache_output.exists()


def test_sealed_next_transition_rejects_atomic_cache_boundary_replacement(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    args, receipt = _prepare_retained_transition(tmp_path)
    original_write = package_plane.exact_write_receipt
    cache = args.produce_owner_package_cache_output
    displaced = tmp_path / "displaced-owned-cache"

    def replace_then_write(path: Path, payload: dict[str, object]) -> tuple[int, int]:
        cache.rename(displaced)
        cache.mkdir(mode=0o700)
        (cache / "attacker").write_bytes(b"preserve")
        return original_write(path, payload)

    monkeypatch.setattr(package_plane, "exact_write_receipt", replace_then_write)
    with pytest.raises(package_plane.VerificationError, match="rollback"):
        package_plane.commit_verification_receipt(args, receipt)
    assert not args.receipt_output.exists()
    assert (cache / "attacker").read_bytes() == b"preserve"
    assert displaced.is_dir()
    assert not args.proposed_package_plane_lock_output.exists()
    assert not args.proposed_ui_owner_lock_output.exists()


def test_sealed_next_transition_rejects_same_size_receipt_mutation_in_final_pass(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    args, receipt = _prepare_retained_transition(tmp_path)
    original_verify_cache = package_plane.verify_open_owner_package_cache

    def verify_cache_then_mutate_receipt(opened: dict[str, object] | None) -> None:
        original_verify_cache(opened)
        retained = args.receipt_output.read_bytes()
        args.receipt_output.write_bytes(b"x" * len(retained))

    monkeypatch.setattr(
        package_plane,
        "verify_open_owner_package_cache",
        verify_cache_then_mutate_receipt,
    )
    with pytest.raises(package_plane.VerificationError, match="final verification"):
        package_plane.commit_verification_receipt(args, receipt)
    assert not args.receipt_output.exists()
    assert not args.proposed_package_plane_lock_output.exists()
    assert not args.proposed_ui_owner_lock_output.exists()
    assert not args.produce_owner_package_cache_output.exists()


@pytest.mark.parametrize("replacement_kind", ("file", "symlink"))
def test_sealed_next_transition_rejects_atomic_receipt_path_replacement(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    replacement_kind: str,
) -> None:
    args, receipt = _prepare_retained_transition(tmp_path)
    original_open = package_plane.open_verified_receipt_output
    attacker = tmp_path / "attacker-receipt-target"
    attacker.write_bytes(b"attacker-preserved")
    replacement = tmp_path / "attacker-receipt-replacement"
    if replacement_kind == "file":
        replacement.write_bytes(b"attacker-preserved")
    else:
        replacement.symlink_to(attacker)

    def replace_then_open(
        path: Path,
        identity: tuple[int, int],
        expected_bytes: bytes,
    ) -> tuple[Path, int, tuple[int, int], bytes]:
        os.replace(replacement, path)
        return original_open(path, identity, expected_bytes)

    monkeypatch.setattr(
        package_plane,
        "open_verified_receipt_output",
        replace_then_open,
    )
    with pytest.raises(package_plane.VerificationError, match="rollback"):
        package_plane.commit_verification_receipt(args, receipt)
    if replacement_kind == "file":
        assert args.receipt_output.read_bytes() == b"attacker-preserved"
    else:
        assert args.receipt_output.is_symlink()
        assert args.receipt_output.readlink() == attacker
    assert not args.proposed_package_plane_lock_output.exists()
    assert not args.proposed_ui_owner_lock_output.exists()
    assert not args.produce_owner_package_cache_output.exists()


def test_sealed_next_transition_rejects_stale_receipt_output_rows(
    tmp_path: Path,
) -> None:
    args, receipt = _prepare_retained_transition(tmp_path)
    receipt["sealedNextAuthorityTransition"]["proposedLockOutputs"][0][
        "sha256"
    ] = "0" * 64
    with pytest.raises(
        package_plane.VerificationError,
        match="receipt proposed lock rows differ",
    ):
        package_plane.commit_verification_receipt(args, receipt)
    assert not args.receipt_output.exists()
    assert not args.proposed_package_plane_lock_output.exists()
    assert not args.proposed_ui_owner_lock_output.exists()
    assert not args.produce_owner_package_cache_output.exists()


def test_sealed_next_transition_second_output_conflict_leaves_no_partial_output(
    tmp_path: Path,
) -> None:
    args = _transition_args(tmp_path)
    validated = package_plane.validate_transition_lock_output_targets(
        args,
        repo_root=REPO_ROOT,
        owner_cache_output=args.produce_owner_package_cache_output,
    )
    assert validated is not None
    args.proposed_ui_owner_lock_output.write_text(
        "appeared", encoding="utf-8"
    )
    with pytest.raises(package_plane.VerificationError, match="target appeared"):
        package_plane.retain_transition_lock_outputs(
            args,
            validated_targets=validated,
            canonical_bytes=b"canonical\n",
            producer_bytes=b"producer\n",
        )
    assert not args.proposed_package_plane_lock_output.exists()
    assert args.proposed_ui_owner_lock_output.read_text(
        encoding="utf-8"
    ) == "appeared"


def test_sealed_next_transition_receipt_failure_rolls_back_outputs_and_cache(
    tmp_path: Path,
) -> None:
    args, receipt = _prepare_retained_transition(tmp_path)
    args.receipt_output.write_text("occupied", encoding="utf-8")
    with pytest.raises(package_plane.VerificationError, match="must be a new"):
        package_plane.commit_verification_receipt(args, receipt)
    assert not args.proposed_package_plane_lock_output.exists()
    assert not args.proposed_ui_owner_lock_output.exists()
    assert not args.produce_owner_package_cache_output.exists()
    with pytest.raises(package_plane.VerificationError, match="cold-input only"):
        package_plane.produce_owner_package_cache(
            SimpleNamespace(
                cold_core_runtime_bundle=None,
                cold_hub_package_plane_receipt=None,
                owner_package_cache=tmp_path,
                transition_from_sealed_preseal=True,
            )
        )


def _write_owner_package_cache_fixture(
    tmp_path: Path,
) -> tuple[dict[str, object], Path, Path]:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    cache = tmp_path / "owner-package-cache"
    authority = cache / "authority"
    packages = cache / "packages"
    destination = tmp_path / "destination-feed"
    authority.mkdir(parents=True)
    packages.mkdir()
    destination.mkdir()

    for plane in (
        lock["coreRuntimeFeed"],
        lock["canonicalOwnerFeed"],
        lock["currentOwnerContractFeed"],
        lock["uiOwnerFeed"],
    ):
        for package in plane["packages"]:
            path = packages / package["fileName"]
            dependencies = ""
            if package["packageId"] == "Chummer.Campaign.Contracts":
                dependencies = (
                    "<dependencies><group targetFramework=\"net10.0\">"
                    "<dependency id=\"Chummer.Engine.Contracts\" version=\""
                    f"{package_plane.CORE_RUNTIME_PACKAGE_VERSION}\" />"
                    "</group></dependencies>"
                )
            nuspec = (
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                "<package><metadata>"
                f"<id>{package['packageId']}</id>"
                f"<version>{package['version']}</version>"
                "<authors>test</authors><description>cache fixture</description>"
                f"{dependencies}"
                "</metadata></package>\n"
            )
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr(f"{package['packageId']}.nuspec", nuspec)
                archive.writestr(
                    f"lib/net10.0/{package['packageId']}.dll", b"cache-fixture"
                )
            package["sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
            package["sizeBytes"] = path.stat().st_size

    fixed_authority_files = {
        "core-lock.json": b'{"fixture":"core-lock"}\n',
        "core-inventory.json": b'{"fixture":"core-inventory"}\n',
        "core-receipt.json": b'{"fixture":"core-receipt"}\n',
        "hub-lock.json": b'{"fixture":"hub-lock"}\n',
        "hub-producer.py": b"# fixture Hub producer\n",
        "legacy-lock.json": b'{"fixture":"legacy-lock"}\n',
        "legacy-producer.py": b"# fixture legacy producer\n",
    }
    for name, content in fixed_authority_files.items():
        (authority / name).write_bytes(content)

    core = lock["coreRuntimeFeed"]
    core["lockSha256"] = hashlib.sha256(
        fixed_authority_files["core-lock.json"]
    ).hexdigest()
    core["inventorySha256"] = hashlib.sha256(
        fixed_authority_files["core-inventory.json"]
    ).hexdigest()
    core["receiptSha256"] = hashlib.sha256(
        fixed_authority_files["core-receipt.json"]
    ).hexdigest()

    hub = lock["canonicalOwnerFeed"]
    hub["lockSha256"] = hashlib.sha256(
        fixed_authority_files["hub-lock.json"]
    ).hexdigest()
    hub["producerSha256"] = hashlib.sha256(
        fixed_authority_files["hub-producer.py"]
    ).hexdigest()
    hub_inventory_path = authority / "hub-inventory.json"
    hub_inventory_path.write_text(
        json.dumps(package_plane.expected_hub_inventory(lock), indent=2) + "\n",
        encoding="utf-8",
    )
    hub["inventorySha256"] = hashlib.sha256(
        hub_inventory_path.read_bytes()
    ).hexdigest()
    hub_receipt_path = authority / "hub-receipt.json"
    hub_receipt_path.write_text(
        json.dumps(
            {
                "contract": hub["receiptContract"],
                "hub_commit": hub["producerCommit"],
                "package_inventory_sha256": hub["inventorySha256"],
                "package_plane_lock_sha256": hub["lockSha256"],
                "package_version": hub["packageVersion"],
                "status": "pass",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    hub["receiptSha256"] = hashlib.sha256(hub_receipt_path.read_bytes()).hexdigest()

    legacy = lock["currentOwnerContractFeed"]
    legacy["lockSha256"] = hashlib.sha256(
        fixed_authority_files["legacy-lock.json"]
    ).hexdigest()
    legacy["producerSha256"] = hashlib.sha256(
        fixed_authority_files["legacy-producer.py"]
    ).hexdigest()
    legacy_inventory_path = authority / "legacy-inventory.json"
    legacy_inventory_path.write_text(
        json.dumps(
            package_plane.expected_current_owner_contract_inventory(lock), indent=2
        )
        + "\n",
        encoding="utf-8",
    )
    legacy["inventorySha256"] = hashlib.sha256(
        legacy_inventory_path.read_bytes()
    ).hexdigest()

    ui_owner = lock["uiOwnerFeed"]
    ui_lock_path = authority / ui_owner["producerLockFileName"]
    ui_lock_path.write_text(
        json.dumps(
            package_plane.build_ui_owner_producer_lock(
                lock,
                recipe_commit=ui_owner["packageRecipeCommit"],
                recipe_sha256=ui_owner["packageRecipeSha256"],
            ),
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )
    ui_owner["producerLockSha256"] = hashlib.sha256(
        ui_lock_path.read_bytes()
    ).hexdigest()
    ui_inventory_path = authority / ui_owner["inventoryFileName"]
    ui_inventory_path.write_text(
        json.dumps(
            package_plane.expected_ui_owner_inventory(lock),
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )
    ui_owner["inventorySha256"] = hashlib.sha256(
        ui_inventory_path.read_bytes()
    ).hexdigest()
    ui_receipt_path = authority / ui_owner["receiptFileName"]
    ui_receipt_path.write_text(
        json.dumps(
            package_plane.expected_ui_owner_receipt(lock),
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )
    ui_owner["receiptSha256"] = hashlib.sha256(
        ui_receipt_path.read_bytes()
    ).hexdigest()

    (cache / "owner-package-cache.json").write_text(
        json.dumps(package_plane.owner_package_cache_manifest(lock), indent=2) + "\n",
        encoding="utf-8",
    )
    return lock, cache, destination


def _write_canonical_core_bundle(
    lock: dict[str, object],
    cache: Path,
    bundle: Path,
    *,
    extra_member: bool = False,
    canonical_metadata: bool = True,
) -> None:
    core = lock["coreRuntimeFeed"]
    members = {
        core["inventoryFileName"]: cache / "authority" / "core-inventory.json",
        core["lockFileName"]: cache / "authority" / "core-lock.json",
        core["receiptFileName"]: cache / "authority" / "core-receipt.json",
        **{
            f"packages/{row['fileName']}": cache / "packages" / row["fileName"]
            for row in core["packages"]
        },
    }
    if extra_member:
        members["unexpected.txt"] = cache / "authority" / "core-lock.json"
    with zipfile.ZipFile(bundle, "w", compression=zipfile.ZIP_STORED) as archive:
        for name in sorted(members):
            info = zipfile.ZipInfo(name)
            info.date_time = (
                package_plane.UI_OWNER_CANONICAL_ZIP_TIMESTAMP
                if canonical_metadata
                else (2026, 8, 29, 0, 0, 0)
            )
            info.compress_type = zipfile.ZIP_STORED
            info.create_system = 3
            info.create_version = 20
            info.extract_version = 20
            info.flag_bits = 0
            info.external_attr = package_plane.UI_OWNER_CANONICAL_ZIP_EXTERNAL_ATTR
            archive.writestr(info, members[name].read_bytes())


def _authorize_test_core_bundle(
    monkeypatch: pytest.MonkeyPatch, bundle: Path
) -> None:
    monkeypatch.setattr(
        package_plane,
        "CORE_RUNTIME_PUBLIC_BUNDLE_SIZE_BYTES",
        bundle.stat().st_size,
    )
    monkeypatch.setattr(
        package_plane,
        "CORE_RUNTIME_PUBLIC_BUNDLE_SHA256",
        hashlib.sha256(bundle.read_bytes()).hexdigest(),
    )


def test_owner_package_cache_import_is_exact_and_copy_only(tmp_path: Path) -> None:
    lock, cache, destination = _write_owner_package_cache_fixture(tmp_path)

    receipts, legacy_receipt, cache_receipt = (
        package_plane.import_owner_package_artifact_cache(lock, cache, destination)
    )

    assert len(list(destination.iterdir())) == 18
    assert receipts["canonicalOwnerFeed"]["status"] == "passed"
    assert receipts["coreRuntimeFeed"]["status"] == "passed"
    assert legacy_receipt["selectedForCanonicalFullFeed"] is True
    assert cache_receipt["cacheKey"] == package_plane.owner_package_cache_manifest(
        lock
    )["cacheKey"]
    assert cache_receipt["importedByCopy"] is True
    assert cache_receipt["packageCount"] == 18
    assert cache_receipt["sourcePath"] == str(cache)


def test_cold_core_bundle_materializes_exact_authority_and_packages(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    lock, cache, _ = _write_owner_package_cache_fixture(tmp_path)
    bundle = tmp_path / "core-runtime-public-bundle.zip"
    _write_canonical_core_bundle(lock, cache, bundle)
    _authorize_test_core_bundle(monkeypatch, bundle)
    core_feed = tmp_path / "core-feed"
    authority = tmp_path / "retained-authority"
    core_feed.mkdir()
    authority.mkdir()

    inventory = package_plane.materialize_cold_core_runtime_bundle(
        lock, bundle, core_feed, authority
    )

    assert inventory["sha256"] == hashlib.sha256(bundle.read_bytes()).hexdigest()
    assert {path.name for path in core_feed.iterdir()} == {
        row["fileName"] for row in lock["coreRuntimeFeed"]["packages"]
    }
    assert {path.name for path in authority.iterdir()} == {
        "core-inventory.json",
        "core-lock.json",
        "core-receipt.json",
    }


def test_cold_core_bundle_rejects_noncanonical_or_extra_members(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    lock, cache, _ = _write_owner_package_cache_fixture(tmp_path)
    bundle = tmp_path / "core-runtime-public-bundle.zip"
    _write_canonical_core_bundle(lock, cache, bundle, extra_member=True)
    _authorize_test_core_bundle(monkeypatch, bundle)
    core_feed = tmp_path / "core-feed"
    authority = tmp_path / "retained-authority"
    core_feed.mkdir()
    authority.mkdir()

    with pytest.raises(
        package_plane.VerificationError,
        match="missing, duplicate, unordered, or extra",
    ):
        package_plane.materialize_cold_core_runtime_bundle(
            lock, bundle, core_feed, authority
        )

    noncanonical = tmp_path / "noncanonical-core-runtime.zip"
    _write_canonical_core_bundle(
        lock, cache, noncanonical, canonical_metadata=False
    )
    _authorize_test_core_bundle(monkeypatch, noncanonical)
    with pytest.raises(package_plane.VerificationError, match="metadata is not canonical"):
        package_plane.materialize_cold_core_runtime_bundle(
            lock, noncanonical, core_feed, authority
        )


def test_cold_core_bundle_rejects_wrong_outer_size_and_digest(
    tmp_path: Path,
) -> None:
    lock = package_plane.fixed_next_authority_oracle_lock(REPO_ROOT)
    core_feed = tmp_path / "core-feed"
    authority = tmp_path / "authority"
    core_feed.mkdir()
    authority.mkdir()
    wrong_size = tmp_path / "wrong-size.zip"
    wrong_size.write_bytes(b"wrong-size")
    with pytest.raises(package_plane.VerificationError, match="outer size differs"):
        package_plane.materialize_cold_core_runtime_bundle(
            lock, wrong_size, core_feed, authority
        )

    wrong_digest = tmp_path / "wrong-digest.zip"
    wrong_digest.write_bytes(
        b"x" * package_plane.CORE_RUNTIME_PUBLIC_BUNDLE_SIZE_BYTES
    )
    with pytest.raises(package_plane.VerificationError, match="outer digest differs"):
        package_plane.materialize_cold_core_runtime_bundle(
            lock, wrong_digest, core_feed, authority
        )


def test_cold_core_bundle_rejects_bytes_swapped_after_authority_capture(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    lock = package_plane.fixed_next_authority_oracle_lock(REPO_ROOT)
    bundle = tmp_path / "core-runtime-public-bundle.zip"
    bundle.write_bytes(b"captured")
    core_feed = tmp_path / "core-feed"
    authority = tmp_path / "authority"
    core_feed.mkdir()
    authority.mkdir()
    monkeypatch.setattr(
        package_plane,
        "require_cold_producer_input",
        lambda *_args, **_kwargs: {
            "path": str(bundle),
            "sha256": package_plane.CORE_RUNTIME_PUBLIC_BUNDLE_SHA256,
            "sizeBytes": package_plane.CORE_RUNTIME_PUBLIC_BUNDLE_SIZE_BYTES,
        },
    )
    monkeypatch.setattr(
        package_plane,
        "secure_regular_file_bytes",
        lambda *_args, **_kwargs: (
            b"x" * package_plane.CORE_RUNTIME_PUBLIC_BUNDLE_SIZE_BYTES
        ),
    )

    with pytest.raises(
        package_plane.VerificationError,
        match="consumed bytes differ from captured authority",
    ):
        package_plane.materialize_cold_core_runtime_bundle(
            lock, bundle, core_feed, authority
        )


def test_cold_core_bundle_rejects_symlink_input(tmp_path: Path) -> None:
    lock, cache, _ = _write_owner_package_cache_fixture(tmp_path)
    bundle = tmp_path / "core-runtime-public-bundle.zip"
    _write_canonical_core_bundle(lock, cache, bundle)
    linked = tmp_path / "linked-core-bundle.zip"
    linked.symlink_to(bundle)
    core_feed = tmp_path / "core-feed"
    authority = tmp_path / "retained-authority"
    core_feed.mkdir()
    authority.mkdir()

    with pytest.raises(package_plane.VerificationError, match="non-symlink"):
        package_plane.materialize_cold_core_runtime_bundle(
            lock, linked, core_feed, authority
        )


def test_cold_hub_receipt_requires_exact_digest_and_payload(tmp_path: Path) -> None:
    lock, cache, _ = _write_owner_package_cache_fixture(tmp_path)
    receipt = cache / "authority" / "hub-receipt.json"

    inventory, content = package_plane.validate_cold_hub_receipt(lock, receipt)

    assert inventory["sha256"] == lock["canonicalOwnerFeed"]["receiptSha256"]
    assert content == receipt.read_bytes()
    lock["canonicalOwnerFeed"]["receiptSha256"] = "0" * 64
    with pytest.raises(package_plane.VerificationError, match="digest differs"):
        package_plane.validate_cold_hub_receipt(lock, receipt)

    altered_receipt = tmp_path / "altered-hub-receipt.json"
    altered = json.loads(content)
    altered["status"] = "failed"
    altered_receipt.write_text(json.dumps(altered), encoding="utf-8")
    lock["canonicalOwnerFeed"]["receiptSha256"] = hashlib.sha256(
        altered_receipt.read_bytes()
    ).hexdigest()
    with pytest.raises(package_plane.VerificationError, match="payload differs"):
        package_plane.validate_cold_hub_receipt(lock, altered_receipt)


def test_cold_hub_receipt_rejects_bytes_swapped_after_authority_capture(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    lock, cache, _ = _write_owner_package_cache_fixture(tmp_path)
    receipt = cache / "authority" / "hub-receipt.json"
    captured = receipt.read_bytes()
    monkeypatch.setattr(
        package_plane,
        "secure_regular_file_bytes",
        lambda *_args, **_kwargs: b"x" * len(captured),
    )

    with pytest.raises(
        package_plane.VerificationError,
        match="consumed bytes differ from captured authority",
    ):
        package_plane.validate_cold_hub_receipt(lock, receipt)


def test_targeted_cache_producer_rejects_mixed_or_partial_inputs(
    tmp_path: Path,
) -> None:
    common = {
        "owner_package_cache": tmp_path / "warm-cache",
        "produce_owner_package_cache_output": tmp_path / "output-cache",
        "repo_root": REPO_ROOT,
        "lock": LOCK,
    }
    with pytest.raises(package_plane.VerificationError, match="choose warm cache or cold"):
        package_plane.produce_owner_package_cache(
            package_plane.argparse.Namespace(
                **common,
                cold_core_runtime_bundle=tmp_path / "core.zip",
                cold_hub_package_plane_receipt=tmp_path / "hub.json",
            )
        )
    with pytest.raises(package_plane.VerificationError, match="requires both"):
        package_plane.produce_owner_package_cache(
            package_plane.argparse.Namespace(
                **{**common, "owner_package_cache": None},
                cold_core_runtime_bundle=tmp_path / "core.zip",
                cold_hub_package_plane_receipt=None,
            )
        )


def test_owner_cache_transaction_rejects_output_inside_consumer_checkout(
    tmp_path: Path,
) -> None:
    repo = tmp_path / "consumer"
    repo.mkdir()
    output = repo / "retained-owner-cache"

    with pytest.raises(package_plane.VerificationError, match="outside the consumer"):
        package_plane.validate_owner_cache_transaction_paths(
            repo.resolve(), output, tmp_path / "receipt.json"
        )


def test_owner_cache_transaction_rejects_receipt_nested_in_retained_cache(
    tmp_path: Path,
) -> None:
    repo = tmp_path / "consumer"
    repo.mkdir()
    output = tmp_path / "retained-owner-cache"

    with pytest.raises(
        package_plane.VerificationError,
        match="outside the retained owner-package cache",
    ):
        package_plane.validate_owner_cache_transaction_paths(
            repo.resolve(), output, output / "receipt.json"
        )


def test_owner_package_cache_rejects_missing_package(tmp_path: Path) -> None:
    lock, cache, destination = _write_owner_package_cache_fixture(tmp_path)
    next((cache / "packages").iterdir()).unlink()

    with pytest.raises(package_plane.VerificationError, match="missing or extra"):
        package_plane.import_owner_package_artifact_cache(lock, cache, destination)


def test_owner_package_cache_rejects_tampered_package(tmp_path: Path) -> None:
    lock, cache, destination = _write_owner_package_cache_fixture(tmp_path)
    package = next((cache / "packages").iterdir())
    with package.open("ab") as stream:
        stream.write(b"tampered")

    with pytest.raises(package_plane.VerificationError, match="package differs"):
        package_plane.import_owner_package_artifact_cache(lock, cache, destination)


def test_owner_package_cache_rejects_tampered_ui_owner_package(tmp_path: Path) -> None:
    lock, cache, destination = _write_owner_package_cache_fixture(tmp_path)
    package = cache / "packages" / lock["packages"][0]["fileName"]
    with package.open("ab") as stream:
        stream.write(b"tampered-ui-owner-package")

    with pytest.raises(package_plane.VerificationError, match="package differs"):
        package_plane.import_owner_package_artifact_cache(lock, cache, destination)


@pytest.mark.parametrize("authority_field", ("receiptFileName", "producerLockFileName"))
def test_owner_package_cache_rejects_tampered_ui_owner_authority_artifact(
    tmp_path: Path,
    authority_field: str,
) -> None:
    lock, cache, destination = _write_owner_package_cache_fixture(tmp_path)
    path = cache / "authority" / lock["uiOwnerFeed"][authority_field]
    with path.open("ab") as stream:
        stream.write(b"tampered-ui-owner-authority")

    with pytest.raises(package_plane.VerificationError, match="artifact differs"):
        package_plane.import_owner_package_artifact_cache(lock, cache, destination)


def test_owner_package_cache_rejects_extra_entry(tmp_path: Path) -> None:
    lock, cache, destination = _write_owner_package_cache_fixture(tmp_path)
    (cache / "unexpected").write_text("extra", encoding="utf-8")

    with pytest.raises(package_plane.VerificationError, match="missing or extra"):
        package_plane.import_owner_package_artifact_cache(lock, cache, destination)


def test_owner_package_cache_rejects_wrong_commit(tmp_path: Path) -> None:
    lock, cache, destination = _write_owner_package_cache_fixture(tmp_path)
    manifest_path = cache / "owner-package-cache.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["authorities"]["hubCanonical"]["producerCommit"] = "f" * 40
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    with pytest.raises(package_plane.VerificationError, match="authority differs"):
        package_plane.import_owner_package_artifact_cache(lock, cache, destination)


def test_ui_owner_producer_lock_is_exact_non_android_and_dependency_bound() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    recipe_commit = "a" * 40
    recipe_sha256 = "b" * 64

    producer_lock = package_plane.build_ui_owner_producer_lock(
        lock,
        recipe_commit=recipe_commit,
        recipe_sha256=recipe_sha256,
    )

    assert producer_lock["contract"] == "chummer6-ui.owner-package-plane-lock/v1"
    assert producer_lock["dependencyAuthorityCacheKey"] == (
        package_plane.upstream_owner_package_cache_manifest(lock)["cacheKey"]
    )
    assert producer_lock["packageRecipeCommit"] == recipe_commit
    assert producer_lock["packageRecipeSha256"] == recipe_sha256
    assert producer_lock["sdkVersion"] == "10.0.103"
    assert [row["packageId"] for row in producer_lock["packages"]] == [
        "Chummer.Campaign.Contracts",
        "Chummer.Ui.Kit",
    ]
    assert producer_lock["packages"][0]["dependencies"] == {
        "Chummer.Engine.Contracts": package_plane.CORE_RUNTIME_PACKAGE_VERSION
    }
    assert producer_lock["packages"][1]["dependencies"] == {}
    assert "android" not in json.dumps(producer_lock).lower()


def test_sealed_ui_owner_recipe_accepts_direct_or_exact_pr_merge_only(
    tmp_path: Path,
) -> None:
    repository = tmp_path / "consumer"
    repository.mkdir()
    subprocess.run(["git", "init", "--quiet"], cwd=repository, check=True)
    subprocess.run(
        ["git", "config", "user.email", "tests@example.invalid"],
        cwd=repository,
        check=True,
    )
    subprocess.run(
        ["git", "config", "user.name", "Tests"], cwd=repository, check=True
    )

    def git(*arguments: str, input_text: str | None = None) -> str:
        return subprocess.run(
            ["git", *arguments],
            cwd=repository,
            check=True,
            capture_output=True,
            text=True,
            input=input_text,
        ).stdout.strip()

    def checkout(commit: str) -> None:
        git("checkout", "--quiet", "--detach", commit)

    def synthetic_commit(tree: str, *parents: str) -> str:
        arguments = ["commit-tree", tree]
        for parent in parents:
            arguments.extend(("-p", parent))
        return git(*arguments, input_text="synthetic merge\n")

    recipe = repository / "recipe.py"
    marker = repository / "marker.txt"
    package_lock = repository / "config" / "package-plane.lock.json"
    producer_lock = repository / "config" / "ui-owner-package-plane.lock.json"
    package_lock.parent.mkdir()
    recipe.write_text("# content-equal recipe\n", encoding="utf-8")
    marker.write_text("base\n", encoding="utf-8")
    package_lock.write_text('{"sealed":false}\n', encoding="utf-8")
    producer_lock.write_text('{"sealed":false}\n', encoding="utf-8")
    git("add", "recipe.py", "marker.txt", "config")
    git("commit", "--quiet", "-m", "base")
    base = git("rev-parse", "HEAD")

    marker.write_text("abandoned\n", encoding="utf-8")
    git("add", "marker.txt")
    git("commit", "--quiet", "-m", "abandoned content-equal recipe")
    abandoned_recipe = git("rev-parse", "HEAD")

    marker.write_text("replacement\n", encoding="utf-8")
    git("add", "marker.txt")
    git("commit", "--quiet", "-m", "replacement recipe")
    direct_recipe = git("rev-parse", "HEAD")
    assert subprocess.run(
        ["git", "diff", "--quiet", abandoned_recipe, direct_recipe, "--", "recipe.py"],
        cwd=repository,
        check=False,
    ).returncode == 0

    package_lock.write_text('{"sealed":true}\n', encoding="utf-8")
    producer_lock.write_text('{"sealed":true}\n', encoding="utf-8")
    git("add", "config")
    git("commit", "--quiet", "-m", "seal")
    sealed = git("rev-parse", "HEAD")
    sealed_tree = git("rev-parse", f"{sealed}^{{tree}}")

    package_plane.require_ui_owner_recipe_authority(
        repository,
        sealed_commit=sealed,
        locked_recipe_commit=direct_recipe,
        producer_lock_recipe_commit=direct_recipe,
    )

    for lock_path in (
        "config/package-plane.lock.json",
        "config/ui-owner-package-plane.lock.json",
    ):
        (repository / lock_path).write_text(
            '{"sealed":"ordinary-dirty"}\n', encoding="utf-8"
        )
        assert git("status", "--porcelain") != ""
        with pytest.raises(
            package_plane.VerificationError, match="sealed preseal topology"
        ):
            package_plane.require_ui_owner_recipe_authority(
                repository,
                sealed_commit=sealed,
                locked_recipe_commit=direct_recipe,
                producer_lock_recipe_commit=direct_recipe,
            )
        git("restore", lock_path)

    marker.write_text("dirty\n", encoding="utf-8")
    with pytest.raises(package_plane.VerificationError, match="sealed preseal topology"):
        package_plane.require_ui_owner_recipe_authority(
            repository,
            sealed_commit=sealed,
            locked_recipe_commit=direct_recipe,
            producer_lock_recipe_commit=direct_recipe,
        )
    git("restore", "marker.txt")

    for lock_path in (
        "config/package-plane.lock.json",
        "config/ui-owner-package-plane.lock.json",
    ):
        for hidden, visible in (
            ("--skip-worktree", "--no-skip-worktree"),
            ("--assume-unchanged", "--no-assume-unchanged"),
        ):
            git("update-index", hidden, lock_path)
            (repository / lock_path).write_text(
                '{"sealed":"masked-dirty"}\n', encoding="utf-8"
            )
            assert git("status", "--porcelain") == ""
            with pytest.raises(
                package_plane.VerificationError, match="sealed preseal topology"
            ):
                package_plane.require_ui_owner_recipe_authority(
                    repository,
                    sealed_commit=sealed,
                    locked_recipe_commit=direct_recipe,
                    producer_lock_recipe_commit=direct_recipe,
                )
            git("update-index", visible, lock_path)
            git("restore", lock_path)

    with pytest.raises(package_plane.VerificationError, match="sealed preseal topology"):
        package_plane.require_ui_owner_recipe_authority(
            repository,
            sealed_commit=sealed,
            locked_recipe_commit=abandoned_recipe,
            producer_lock_recipe_commit=abandoned_recipe,
        )
    with pytest.raises(package_plane.VerificationError, match="sealed preseal topology"):
        package_plane.require_ui_owner_recipe_authority(
            repository,
            sealed_commit=sealed,
            locked_recipe_commit=direct_recipe,
            producer_lock_recipe_commit=abandoned_recipe,
        )

    pull_request_merge = synthetic_commit(sealed_tree, base, sealed)
    checkout(pull_request_merge)
    package_plane.require_ui_owner_recipe_authority(
        repository,
        sealed_commit=pull_request_merge,
        locked_recipe_commit=direct_recipe,
        producer_lock_recipe_commit=direct_recipe,
    )

    direct_recipe_tree = git("rev-parse", f"{direct_recipe}^{{tree}}")
    sibling_recipe = synthetic_commit(direct_recipe_tree, abandoned_recipe)
    sibling_sealed = synthetic_commit(sealed_tree, sibling_recipe)
    assert sibling_recipe != direct_recipe
    assert sibling_sealed != sealed
    assert git("rev-parse", f"{sibling_recipe}^{{tree}}") == direct_recipe_tree
    assert git("rev-parse", f"{sibling_sealed}^{{tree}}") == sealed_tree

    checkout(sealed)
    with pytest.raises(package_plane.VerificationError, match="sealed preseal topology"):
        package_plane.require_ui_owner_recipe_authority(
            repository,
            sealed_commit=sealed,
            locked_recipe_commit=sibling_recipe,
            producer_lock_recipe_commit=sibling_recipe,
        )

    sibling_pull_request_merge = synthetic_commit(sealed_tree, base, sibling_sealed)
    checkout(sibling_pull_request_merge)
    with pytest.raises(package_plane.VerificationError, match="sealed preseal topology"):
        package_plane.require_ui_owner_recipe_authority(
            repository,
            sealed_commit=sibling_pull_request_merge,
            locked_recipe_commit=direct_recipe,
            producer_lock_recipe_commit=direct_recipe,
        )

    for invalid_seal in (
        synthetic_commit(sealed_tree),
        synthetic_commit(sealed_tree, base),
        synthetic_commit(sealed_tree, direct_recipe, base),
    ):
        invalid_pull_request_merge = synthetic_commit(sealed_tree, base, invalid_seal)
        checkout(invalid_pull_request_merge)
        with pytest.raises(
            package_plane.VerificationError, match="sealed preseal topology"
        ):
            package_plane.require_ui_owner_recipe_authority(
                repository,
                sealed_commit=invalid_pull_request_merge,
                locked_recipe_commit=direct_recipe,
                producer_lock_recipe_commit=direct_recipe,
            )

    reversed_merge = synthetic_commit(sealed_tree, sealed, base)
    checkout(reversed_merge)
    with pytest.raises(package_plane.VerificationError, match="sealed preseal topology"):
        package_plane.require_ui_owner_recipe_authority(
            repository,
            sealed_commit=reversed_merge,
            locked_recipe_commit=direct_recipe,
            producer_lock_recipe_commit=direct_recipe,
        )
    git("replace", reversed_merge, pull_request_merge)
    with pytest.raises(package_plane.VerificationError, match="sealed preseal topology"):
        package_plane.require_ui_owner_recipe_authority(
            repository,
            sealed_commit=reversed_merge,
            locked_recipe_commit=direct_recipe,
            producer_lock_recipe_commit=direct_recipe,
        )
    git("replace", "-d", reversed_merge)

    unrelated = synthetic_commit(sealed_tree)
    unrelated_merge = synthetic_commit(sealed_tree, unrelated, sealed)
    checkout(unrelated_merge)
    with pytest.raises(package_plane.VerificationError, match="sealed preseal topology"):
        package_plane.require_ui_owner_recipe_authority(
            repository,
            sealed_commit=unrelated_merge,
            locked_recipe_commit=direct_recipe,
            producer_lock_recipe_commit=direct_recipe,
        )

    extra_parent_merge = synthetic_commit(sealed_tree, base, sealed, unrelated)
    checkout(extra_parent_merge)
    with pytest.raises(package_plane.VerificationError, match="sealed preseal topology"):
        package_plane.require_ui_owner_recipe_authority(
            repository,
            sealed_commit=extra_parent_merge,
            locked_recipe_commit=direct_recipe,
            producer_lock_recipe_commit=direct_recipe,
        )

    checkout(sealed)
    package_lock.write_text('{"sealed":"tampered"}\n', encoding="utf-8")
    git("add", "config/package-plane.lock.json")
    git("commit", "--quiet", "-m", "tampered merge tree")
    tampered_tree = git("rev-parse", "HEAD^{tree}")
    nonmatching_merge = synthetic_commit(tampered_tree, base, sealed)
    checkout(nonmatching_merge)
    with pytest.raises(package_plane.VerificationError, match="sealed preseal topology"):
        package_plane.require_ui_owner_recipe_authority(
            repository,
            sealed_commit=nonmatching_merge,
            locked_recipe_commit=direct_recipe,
            producer_lock_recipe_commit=direct_recipe,
        )

    checkout(pull_request_merge)
    with pytest.raises(package_plane.VerificationError, match="sealed preseal topology"):
        package_plane.require_ui_owner_recipe_authority(
            repository,
            sealed_commit=sealed,
            locked_recipe_commit=direct_recipe,
            producer_lock_recipe_commit=direct_recipe,
        )


def _write_ui_owner_identity_fixture(
    path: Path,
    *,
    package_id: str = "Chummer.Campaign.Contracts",
    version: str = "0.1.0-preview",
    dependency_id: str = "Chummer.Engine.Contracts",
    dependency_version: str | None = None,
) -> None:
    dependency_version = dependency_version or package_plane.CORE_RUNTIME_PACKAGE_VERSION
    nuspec = (
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
        "<package><metadata>"
        f"<id>{package_id}</id><version>{version}</version>"
        "<authors>test</authors><description>fixture</description>"
        "<dependencies><group targetFramework=\"net10.0\">"
        f"<dependency id=\"{dependency_id}\" version=\"{dependency_version}\" />"
        "</group></dependencies></metadata></package>"
    )
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec)
        archive.writestr(f"lib/net10.0/{package_id}.dll", b"fixture")


@pytest.mark.parametrize(
    ("changed", "message"),
    (
        ({"package_id": "Wrong.Contracts"}, "identity differs"),
        ({"version": "0.1.1"}, "identity differs"),
        ({"dependency_id": "Wrong.Contracts"}, "dependencies differ"),
        ({"dependency_version": "[0.0.1, )"}, "dependencies differ"),
    ),
)
def test_ui_owner_package_identity_rejects_wrong_metadata(
    tmp_path: Path,
    changed: dict[str, str],
    message: str,
) -> None:
    package = tmp_path / "candidate.nupkg"
    _write_ui_owner_identity_fixture(package, **changed)

    with pytest.raises(package_plane.VerificationError, match=message):
        package_plane.require_package_identity(
            package,
            package_id="Chummer.Campaign.Contracts",
            version="0.1.0-preview",
            dependencies={
                "Chummer.Engine.Contracts": package_plane.CORE_RUNTIME_PACKAGE_VERSION
            },
        )


def _write_noncanonical_ui_owner_package(
    path: Path,
    *,
    salt: str,
    timestamp: tuple[int, int, int, int, int, int],
    dll_bytes: bytes = b"deterministic-owner-assembly",
    version: str = "0.1.0-preview",
) -> bytes:
    package_id = "Chummer.Campaign.Contracts"
    dependency_version = package_plane.CORE_RUNTIME_PACKAGE_VERSION
    nuspec = (
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
        "<package><metadata>"
        f"<id>{package_id}</id><version>{version}</version>"
        "<authors>test</authors><description>fixture</description>"
        "<dependencies><group targetFramework=\"net10.0\">"
        "<dependency id=\"Chummer.Engine.Contracts\" version=\""
        f"{dependency_version}\" />"
        "</group></dependencies></metadata></package>"
    ).encode("utf-8")
    core_name = (
        "package/services/metadata/core-properties/"
        f"{hashlib.sha256(salt.encode()).hexdigest()[:32]}.psmdcp"
    )
    payloads = [
        (core_name, f"host-specific-core-{salt}".encode()),
        ("[Content_Types].xml", b"<Types />"),
        (f"lib/net10.0/{package_id}.dll", dll_bytes),
        (f"{package_id}.nuspec", nuspec),
        ("_rels/.rels", f"host-specific-relations-{salt}".encode()),
    ]
    if salt.endswith("b"):
        payloads.reverse()
    with zipfile.ZipFile(path, "w") as archive:
        for name, payload in payloads:
            info = zipfile.ZipInfo(name, date_time=timestamp)
            info.create_system = 0 if salt.endswith("a") else 3
            info.external_attr = 0 if salt.endswith("a") else 0o100600 << 16
            archive.writestr(info, payload)
    return nuspec


def test_ui_owner_double_cold_produce_normalizes_to_identical_sha_and_size(
    tmp_path: Path,
) -> None:
    first_root = tmp_path / "cold-a"
    second_root = tmp_path / "cold-b"
    first_root.mkdir()
    second_root.mkdir()
    first = first_root / "Chummer.Campaign.Contracts.0.1.0-preview.nupkg"
    second = second_root / "Chummer.Campaign.Contracts.0.1.0-preview.nupkg"
    nuspec = _write_noncanonical_ui_owner_package(
        first, salt="host-a", timestamp=(2025, 1, 2, 3, 4, 6)
    )
    _write_noncanonical_ui_owner_package(
        second, salt="host-b", timestamp=(2026, 8, 29, 12, 30, 0)
    )
    dependencies = {
        "Chummer.Engine.Contracts": package_plane.CORE_RUNTIME_PACKAGE_VERSION
    }

    for package in (first, second):
        package_plane.normalize_ui_owner_package(
            package,
            package_id="Chummer.Campaign.Contracts",
            version="0.1.0-preview",
            dependencies=dependencies,
        )

    assert hashlib.sha256(first.read_bytes()).hexdigest() == hashlib.sha256(
        second.read_bytes()
    ).hexdigest()
    assert first.stat().st_size == second.stat().st_size
    with zipfile.ZipFile(first) as archive:
        assert archive.read("Chummer.Campaign.Contracts.nuspec") == nuspec


def test_ui_owner_normalization_preserves_payload_tamper_in_byte_identity(
    tmp_path: Path,
) -> None:
    first = tmp_path / "first.nupkg"
    tampered = tmp_path / "tampered.nupkg"
    _write_noncanonical_ui_owner_package(
        first, salt="host-a", timestamp=(2025, 1, 2, 3, 4, 6)
    )
    _write_noncanonical_ui_owner_package(
        tampered,
        salt="host-b",
        timestamp=(2026, 8, 29, 12, 30, 0),
        dll_bytes=b"tampered-owner-assembly",
    )
    dependencies = {
        "Chummer.Engine.Contracts": package_plane.CORE_RUNTIME_PACKAGE_VERSION
    }
    for package in (first, tampered):
        package_plane.normalize_ui_owner_package(
            package,
            package_id="Chummer.Campaign.Contracts",
            version="0.1.0-preview",
            dependencies=dependencies,
        )
    assert hashlib.sha256(first.read_bytes()).hexdigest() != hashlib.sha256(
        tampered.read_bytes()
    ).hexdigest()


def test_ui_owner_normalization_rejects_wrong_nuspec_before_rewrite(
    tmp_path: Path,
) -> None:
    package = tmp_path / "wrong-version.nupkg"
    _write_noncanonical_ui_owner_package(
        package,
        salt="host-a",
        timestamp=(2025, 1, 2, 3, 4, 6),
        version="0.1.1",
    )
    before = package.read_bytes()

    with pytest.raises(package_plane.VerificationError, match="identity differs"):
        package_plane.normalize_ui_owner_package(
            package,
            package_id="Chummer.Campaign.Contracts",
            version="0.1.0-preview",
            dependencies={
                "Chummer.Engine.Contracts": package_plane.CORE_RUNTIME_PACKAGE_VERSION
            },
        )
    assert package.read_bytes() == before


def test_ui_owner_pack_uses_deterministic_build_and_archive_normalization() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    pack = source.index('"pack",', source.index("def produce_ui_owner_packages("))
    normalize = source.index("normalize_ui_owner_package(", pack)
    inventory = source.index("secure_regular_file_inventory(", normalize)

    assert pack < normalize < inventory
    for property_value in (
        "-p:ContinuousIntegrationBuild=true",
        "-p:Deterministic=true",
        "-p:DeterministicSourcePaths=true",
        "-p:PathMap=",
        "-p:UseSharedCompilation=false",
    ):
        assert property_value in source[pack:normalize]


def test_checked_in_lock_and_consumer_source_digests_are_current() -> None:
    lock = package_plane.load_json(LOCK)
    package_plane.validate_lock(lock)
    package_plane.validate_test_compile_items(REPO_ROOT)
    rows = package_plane.verify_source_files(REPO_ROOT, lock["consumer"]["sourceFiles"])
    assert len(rows) == len(lock["consumer"]["sourceFiles"])


def test_forged_owner_pin_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["owners"][0]["commit"] = "main"
    with pytest.raises(package_plane.VerificationError, match="owner commit is not exact"):
        package_plane.validate_lock(lock)


def test_well_formed_but_substituted_owner_authority_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["owners"][0]["commit"] = "f" * 40
    with pytest.raises(package_plane.VerificationError, match="fixed authority"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["packages"][0]["commit"] = "f" * 40
    lock["uiOwnerFeed"]["packages"][0]["commit"] = "f" * 40
    with pytest.raises(package_plane.VerificationError, match="source authority"):
        package_plane.validate_lock(lock)


def test_ui_owner_cache_hit_skips_producer_and_cold_path_uses_same_recipe() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    cache_branch = source.index("if cached_feed_receipts is not None:")
    cold_branch = source.index("else:", cache_branch)
    producer_call = source.index("produce_ui_owner_packages(", cold_branch)
    expected_names = source.index("expected_names =", cold_branch)

    assert cache_branch < cold_branch < producer_call < expected_names
    assert "for package in lock[\"packages\"]:" not in source[cold_branch:expected_names]
    assert "ui_inventory != expected_ui_owner_inventory(lock)" in source[
        producer_call:expected_names
    ]
    assert "ui_producer_receipt != expected_ui_owner_receipt(lock)" in source[
        producer_call:expected_names
    ]

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["owners"][0]["repository"] = "https://github.com/ArchonMegalon/substitute.git"
    with pytest.raises(package_plane.VerificationError, match="fixed authority"):
        package_plane.validate_lock(lock)


def test_substituted_hub_canonical_feed_authority_is_rejected(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["canonicalOwnerFeed"]["packages"][0]["sha256"] = "f" * 64
    with pytest.raises(package_plane.VerificationError, match="canonical package authority"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["canonicalOwnerFeed"]["producerCommit"] = "f" * 40
    with pytest.raises(package_plane.VerificationError, match="fixed feed"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    engine = lock["coreRuntimeFeed"]["packages"][0]
    overlapping_engine = {
        "fileName": engine["fileName"],
        "ownerDirectory": "chummer-core-engine",
        "packageId": engine["packageId"],
        "project": engine["project"],
        "version": engine["version"],
    }
    lock["packages"].append(overlapping_engine)
    expected_packages = dict(package_plane.EXPECTED_PACKAGES)
    expected_packages["Chummer.Engine.Contracts"] = (
        overlapping_engine["ownerDirectory"],
        overlapping_engine["project"],
        overlapping_engine["fileName"],
        overlapping_engine["version"],
    )
    monkeypatch.setattr(package_plane, "EXPECTED_PACKAGES", expected_packages)
    with pytest.raises(
        package_plane.VerificationError,
        match="UI-owner package rows|Core, Hub, and UI",
    ):
        package_plane.validate_lock(lock)


def test_substituted_core_runtime_authority_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["coreRuntimeFeed"]["packageRecipeCommit"] = "f" * 40
    with pytest.raises(package_plane.VerificationError, match="fixed feed"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["coreRuntimeFeed"]["packages"][0]["sha256"] = "f" * 64
    with pytest.raises(package_plane.VerificationError, match="bytes differ"):
        package_plane.validate_lock(lock)


def test_canonical_and_ui_package_planes_are_exact_atomic_and_disjoint() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    canonical = lock["canonicalOwnerFeed"]
    canonical_ids = {row["packageId"] for row in canonical["packages"]}
    core = lock["coreRuntimeFeed"]
    core_ids = {row["packageId"] for row in core["packages"]}
    ui_ids = {row["packageId"] for row in lock["packages"]}

    assert canonical["lockContract"] == "chummer-hub.package-plane-lock/v5"
    assert canonical["inventoryContract"] == "chummer-hub.external-package-inventory/v4"
    assert canonical_ids == {
        "Chummer.Hub.Registry.Contracts",
        "Chummer.Play.Contracts",
        "Chummer.Run.Contracts",
        "Chummer.Run.Registry",
    }
    assert core_ids == {
        "Chummer.Application",
        "Chummer.Engine.Contracts",
        "Chummer.Engine.GmCharacterEdits",
        "Chummer.Infrastructure",
        "Chummer.Rulesets.Hosting",
        "Chummer.Rulesets.Sr4",
        "Chummer.Rulesets.Sr5",
        "Chummer.Rulesets.Sr6",
    }
    assert ui_ids == {
        "Chummer.Campaign.Contracts",
        "Chummer.Ui.Kit",
    }
    assert canonical_ids.isdisjoint(ui_ids)
    assert canonical_ids.isdisjoint(core_ids)
    assert core_ids.isdisjoint(ui_ids)
    assert len(canonical_ids | core_ids | ui_ids) == 14
    assert all(
        {"repository", "commit", "project"}.issubset(row)
        for row in canonical["packages"]
    )

    current = lock["currentOwnerContractFeed"]
    assert {row["packageId"] for row in current["packages"]} == {
        "Chummer.Engine.Contracts",
        "Chummer.Hub.Registry.Contracts",
        "Chummer.Play.Contracts",
        "Chummer.Run.Contracts",
    }
    current_receipt = package_plane.current_owner_contract_feed_binding_receipt(lock)
    assert current_receipt["selectedForCoreRuntimeCompatibility"] is True
    assert current_receipt["selectedForCanonicalFullFeed"] is False
    assert current_receipt["status"] == "bound_not_selected"

    assert lock["canonicalOwnerFeed"]["producerCommit"] == (
        "bc199cbe0982833ec2fc9ce625826e612759d67a"
    )
    assert lock["uiOwnerFeed"]["packages"][0]["commit"] == (
        "bc199cbe0982833ec2fc9ce625826e612759d67a"
    )
    assert core["packageRecipeCommit"] == (
        "c06f22c185c7b733637fdb76b3cf333f31716781"
    )
    assert core["runtimeSourceCommit"] == (
        "60112dccb6a3faad330d32c3c98eef0aa81d97af"
    )
    assert "3b72367cc13e76d3d50db9eeec3224785037fb5e" not in SCRIPT.read_text(
        encoding="utf-8"
    )


def test_mutable_external_package_source_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["externalPackages"][0]["source"] = "https://api.nuget.org/v3/index.json"
    with pytest.raises(package_plane.VerificationError, match="immutable NuGet path"):
        package_plane.validate_lock(lock)


def test_missing_core_runtime_package_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["coreRuntimeFeed"]["packages"][-1]["packageId"] = "Chummer.Rulesets.Sr7"
    with pytest.raises(package_plane.VerificationError, match="set or order"):
        package_plane.validate_lock(lock)


def test_reduced_consumer_digest_or_build_set_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["consumer"]["sourceFiles"].pop("Chummer.Avalonia/Chummer.Avalonia.csproj")
    with pytest.raises(package_plane.VerificationError, match="source-file set"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["consumer"]["buildProjects"].pop()
    with pytest.raises(package_plane.VerificationError, match="build project set"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["consumer"]["testProjects"].pop()
    with pytest.raises(package_plane.VerificationError, match="test project set"):
        package_plane.validate_lock(lock)


def test_product_unit_test_compile_set_rejects_an_extra_source(tmp_path: Path) -> None:
    project_dir = tmp_path / "Chummer.Product.UnitTests"
    project_dir.mkdir()
    source = (REPO_ROOT / "Chummer.Product.UnitTests" / "Chummer.Product.UnitTests.csproj").read_text(
        encoding="utf-8"
    )
    (project_dir / "Chummer.Product.UnitTests.csproj").write_text(
        source.replace(
            "</Project>",
            '  <ItemGroup><Compile Include="InjectedTests.cs" /></ItemGroup>\n</Project>',
        ),
        encoding="utf-8",
    )
    with pytest.raises(package_plane.VerificationError, match="compile source set"):
        package_plane.validate_test_compile_items(tmp_path)

    (project_dir / "Chummer.Product.UnitTests.csproj").write_text(
        source.replace(
            "<EnableDefaultCompileItems>false</EnableDefaultCompileItems>",
            "<EnableDefaultCompileItems Condition=\"'$(Injected)' == '1'\">false</EnableDefaultCompileItems>",
        ),
        encoding="utf-8",
    )
    with pytest.raises(package_plane.VerificationError, match="disable default compile globs"):
        package_plane.validate_test_compile_items(tmp_path)

    (project_dir / "Chummer.Product.UnitTests.csproj").write_text(
        source.replace(
            "  <ItemGroup>\n    <Compile Include=\"DesktopUpdateArtifactTests.cs\" />",
            "  <ItemGroup Condition=\"'$(Injected)' == '1'\">\n"
            "    <Compile Include=\"DesktopUpdateArtifactTests.cs\" />",
        ),
        encoding="utf-8",
    )
    with pytest.raises(package_plane.VerificationError, match="must be unconditional"):
        package_plane.validate_test_compile_items(tmp_path)


def test_child_environment_drops_ambient_msbuild_nuget_and_chummer_inputs(
    tmp_path: Path,
) -> None:
    malicious = tmp_path / "malicious-path"
    malicious.mkdir()
    malicious_marker = tmp_path / "malicious-executed"
    for name in ("bash", "dotnet", "git", "python3"):
        executable = malicious / name
        executable.write_text(
            f"#!/bin/sh\nprintf hit > '{malicious_marker}'\nexit 99\n",
            encoding="utf-8",
        )
        executable.chmod(0o700)
    trusted_dotnet_root = tmp_path / "trusted-dotnet"
    trusted_dotnet_root.mkdir()
    trusted_dotnet = trusted_dotnet_root / "dotnet"
    trusted_dotnet.write_text("#!/bin/sh\nexit 0\n", encoding="utf-8")
    trusted_dotnet.chmod(0o700)
    parent = {
        "PATH": f"{malicious}:{os.environ['PATH']}",
        "HTTP_PROXY": "http://network-proxy.invalid:8080",
        "DirectoryBuildPropsPath": "/tmp/injected.props",
        "CustomBeforeMicrosoftCommonTargets": "/tmp/injected.targets",
        "MSBuildSDKsPath": "/tmp/injected-sdks",
        "RestoreSources": "https://packages.invalid/v3/index.json",
        "CHUMMER_CONTRACTS_PACKAGE_VERSION": "999.0.0",
        "NUGET_PACKAGES": "/tmp/ambient-packages",
        "NUGET_CREDENTIALPROVIDERS_PATH": "/tmp/injected-provider",
        "BASH_ENV": "/tmp/injected-bash-env",
        "LD_PRELOAD": "/tmp/injected.so",
    }

    environment = package_plane.isolated_child_environment(
        tmp_path / "caches",
        parent,
        trusted_dotnet_root=trusted_dotnet_root,
    )

    assert environment["HTTP_PROXY"] == parent["HTTP_PROXY"]
    assert environment["PATH"] == (
        f"{trusted_dotnet_root}:{package_plane.TRUSTED_SYSTEM_PATH}"
    )
    assert str(malicious) not in environment["PATH"]
    assert subprocess.run(
        ["dotnet", "--version"],
        env=environment,
        check=False,
    ).returncode == 0
    assert not malicious_marker.exists()
    assert Path(environment["NUGET_PACKAGES"]).is_relative_to(tmp_path)
    for name in (
        "DirectoryBuildPropsPath",
        "CustomBeforeMicrosoftCommonTargets",
        "MSBuildSDKsPath",
        "RestoreSources",
        "CHUMMER_CONTRACTS_PACKAGE_VERSION",
        "NUGET_CREDENTIALPROVIDERS_PATH",
        "BASH_ENV",
        "LD_PRELOAD",
    ):
        assert name not in environment


def test_owner_pack_and_consumer_restore_reject_version_approximation() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    assert "-p:ChummerCoreRuntimePackageVersion={CORE_RUNTIME_PACKAGE_VERSION}" in source
    assert (
        "-p:ChummerEngineContractsPackageVersion="
        "{CANONICAL_ENGINE_CONTRACTS_VERSION}" in source
    )
    assert "-p:ChummerLocalContractsProject=" in source
    assert "-p:ChummerUseLocalCompatibilityTree=false" in source
    assert "-p:RestoreLockedMode=false" not in source
    assert "-p:RestorePackagesWithLockFile=false" not in source
    assert source.count("-p:RestoreLockedMode=true") == 0
    assert "canonical_feed_receipts = import_hub_canonical_feed(" in source
    assert "current_owner_contract_feed_receipt = import_current_owner_contract_feed(" in source
    assert '"compatibilityPurpose": "exact-core-runtime-transitive-dependencies"' in source
    assert "if package[\"packageId\"] in HUB_CANONICAL_PACKAGE_IDS:" not in source
    assert source.count("-warnaserror:NU1603,NU1608") == 3
    assert source.count("-p:WarningsAsErrors=NU1603%3BNU1608") == 1
    assert source.count('"--minimum-expected-tests"') == 3
    assert source.count('"--no-progress"') == 3
    for authority in (
        "-p:RestoreSources={feed}",
        "-p:RestoreAdditionalProjectSources=",
        "-p:RestoreConfigFile={pack_config}",
        "-p:RestoreFallbackFolders=",
        "-p:RestoreIgnoreFailedSources=false",
    ):
        assert authority in source

    props = (REPO_ROOT / "Directory.Build.props").read_text(encoding="utf-8")
    helper = (REPO_ROOT / "scripts" / "ai" / "with-package-plane.sh").read_text(
        encoding="utf-8"
    )
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    core_version = lock["coreRuntimeFeed"]["packageVersion"]
    hub_version = lock["canonicalOwnerFeed"]["packageVersion"]
    assert (
        "<ChummerContractsPackageVersion Condition=\"'$(ChummerContractsPackageVersion)' == ''\">"
        f"{core_version}"
        "</ChummerContractsPackageVersion>"
    ) in props
    assert (
        "<ChummerCoreRuntimePackageVersion Condition=\"'$(ChummerCoreRuntimePackageVersion)' == ''\">"
        f"{core_version}"
        "</ChummerCoreRuntimePackageVersion>"
    ) in props
    assert (
        "<ChummerRunContractsPackageVersion Condition=\"'$(ChummerRunContractsPackageVersion)' == ''\">"
        f"{hub_version}"
        "</ChummerRunContractsPackageVersion>"
    ) in props
    assert (
        "<ChummerHubRegistryContractsPackageVersion Condition=\"'$(ChummerHubRegistryContractsPackageVersion)' == ''\">"
        f"{hub_version}"
        "</ChummerHubRegistryContractsPackageVersion>"
    ) in props
    assert 'configured_contracts_version="${CHUMMER_CONTRACTS_PACKAGE_VERSION:-}"' in helper
    assert (
        'contracts_version="${configured_contracts_version:-'
        f'{core_version}}}"' in helper
    )
    assert (
        'core_runtime_version="${CHUMMER_CORE_RUNTIME_PACKAGE_VERSION:-'
        f'{core_version}}}"' in helper
    )
    assert (
        'run_contracts_version="${configured_run_contracts_version:-'
        f'{hub_version}}}"' in helper
    )
    assert (
        'hub_registry_contracts_version="${configured_hub_registry_contracts_version:-'
        f'{hub_version}}}"' in helper
    )
    assert (
        "'-p:NuGetLockFilePath=$(BaseIntermediateOutputPath)"
        "packages.local-tree.lock.json'"
    ) in helper
    assert "5.225.0.0" not in props
    assert "5.225.0.0" not in helper


def test_fresh_package_plane_executes_all_career_mutation_parity_suites() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    assembly_info = (
        REPO_ROOT / "Chummer.Presentation" / "AssemblyInfo.cs"
    ).read_text(encoding="utf-8")
    assert (
        '"Chummer.Product.UnitTests/Chummer.Product.UnitTests.csproj"'
        in source
    )
    assert "Chummer.Tests/Presentation/CareerActiveSkillAdvanceParityTests.cs|" in source
    assert "Chummer.Tests/Presentation/CareerSkillGroupAdvanceParityTests.cs|" in source
    assert "Chummer.Tests/Presentation/CareerSkillSpecializationParityTests.cs|" in source
    assert "Chummer.Tests/Presentation/CareerWeaponFireParityTests.cs" in source
    assert "Chummer.Tests/Presentation/WorkspaceOverviewLoaderTests.cs" in source
    assert "FullyQualifiedName~CareerActiveSkillAdvanceParityTests|" in source
    assert "FullyQualifiedName~CareerSkillGroupAdvanceParityTests|" in source
    assert "FullyQualifiedName~CareerSkillSpecializationParityTests|" in source
    assert "FullyQualifiedName~CareerWeaponFireParityTests" in source
    assert "FOCUSED_CAREER_ADVANCE_MINIMUM_TESTS = 19" in source
    assert '"focusedCareerAdvanceTestExecution": focused_career_advance_execution' in source
    assert 'InternalsVisibleTo("Chummer.Product.UnitTests")' in assembly_info
    focused_execution = source.split(
        "focused_test_assembly_path = consumer / PRODUCT_TEST_ASSEMBLY", 1
    )[1].split("after = package_inventory", 1)[0]
    assert '"--disable-build-servers"' not in focused_execution
    assert focused_execution.count('"reuseFullSuiteBuild": True') == 2
    assert focused_execution.count('"runner": "direct-exact-assembly"') == 2
    assert focused_execution.count('"testAssembly": focused_test_assembly') == 2
    assert focused_execution.count('str(sdk_root / "dotnet")') == 2
    assert focused_execution.count("str(focused_test_assembly_path)") == 2
    assert '"--no-build"' not in focused_execution
    assert '"--no-restore"' not in focused_execution
    assert (
        'raise VerificationError(\n'
        '                    "focused tests did not reuse the exact full-suite assembly"'
        in focused_execution
    )


def test_full_product_test_compile_is_serialized_without_shared_compiler() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    full_suite_execution = source.split(
        "test_executions: list[dict[str, Any]] = []", 1
    )[1].split(
        "focused_test_assembly_path = consumer / PRODUCT_TEST_ASSEMBLY", 1
    )[0]
    assert full_suite_execution.count('"-m:1"') == 1
    assert full_suite_execution.count('"-p:BuildInParallel=false"') == 1
    assert full_suite_execution.count('"-p:UseSharedCompilation=false"') == 1
    assert full_suite_execution.count('"--disable-build-servers"') == 1
    assert '"buildInParallel": False' in full_suite_execution
    assert '"disableBuildServers": True' in full_suite_execution
    assert '"maxCpuCount": 1' in full_suite_execution
    assert '"useSharedCompilation": False' in full_suite_execution
    assert '"compileRunner": "serialized-package-plane-build"' in full_suite_execution
    assert '"runner": "direct-exact-assembly"' in full_suite_execution
    assert 'FULL_PRODUCT_TEST_MINIMUM_TESTS = 170' in source
    full_suite_runner = full_suite_execution.split(
        'full_test_execution = {', 1
    )[1]
    assert full_suite_runner.count('str(sdk_root / "dotnet")') == 1
    assert full_suite_runner.count("str(full_test_assembly_path)") == 1
    assert '"-m:1"' not in full_suite_runner
    assert '"--disable-build-servers"' not in full_suite_runner
    assert '"--minimum-expected-tests"' in full_suite_runner


def test_exact_product_test_assembly_rejects_wrong_path_and_tampered_identity(
    tmp_path: Path,
) -> None:
    consumer = tmp_path / "consumer"
    expected_path = consumer / package_plane.PRODUCT_TEST_ASSEMBLY
    expected_path.parent.mkdir(parents=True)
    expected_path.write_bytes(b"exact-test-assembly")
    wrong_path = expected_path.with_name("wrong.dll")
    wrong_path.write_bytes(b"wrong-test-assembly")

    with pytest.raises(
        package_plane.VerificationError,
        match="path differs from authority",
    ):
        package_plane.exact_product_test_assembly_inventory(consumer, wrong_path)

    inventory = package_plane.exact_product_test_assembly_inventory(
        consumer,
        expected_path,
    )
    expected_path.write_bytes(b"tampered-test-assembly")
    with pytest.raises(
        package_plane.VerificationError,
        match="identity changed",
    ):
        package_plane.exact_product_test_assembly_inventory(
            consumer,
            expected_path,
            expected=inventory,
        )


def test_fresh_package_plane_executes_overview_activation_regression_without_fake_timing_claim() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    assert package_plane.FOCUSED_OVERVIEW_TEST_FILTER == (
        "FullyQualifiedName~WorkspaceOverviewLoaderTests"
    )
    assert package_plane.FOCUSED_OVERVIEW_MINIMUM_TESTS == 19
    assert package_plane.CREATION_INITIAL_AUTHORITY_BUDGET_SECONDS == 90
    assert '"measurementClaimed": False' in source
    assert '"requiresHostedWallClockMeasurement": True' in source
    assert (
        "Initial_creation_activation_attempt_bypasses_workspace_and_domain_reload_path"
        in source
    )


def test_local_compatibility_project_defaults_are_overrideable() -> None:
    root = ET.parse(REPO_ROOT / "Directory.Build.props").getroot()
    for property_name in (
        "ChummerLocalContractsProject",
        "ChummerLocalCampaignContractsProject",
        "ChummerLocalHubRegistryContractsProject",
        "ChummerLocalRunContractsProject",
        "ChummerLocalUiKitProject",
        "ChummerLocalMediaContractsProject",
    ):
        nodes = root.findall(f"./PropertyGroup/{property_name}")
        assert len(nodes) == 1
        assert nodes[0].attrib.get("Condition") == f"'$({property_name})' == ''"


def test_local_source_graph_uses_locked_owner_packages_once() -> None:
    props = (REPO_ROOT / "Directory.Build.props").read_text(encoding="utf-8")
    helper = (REPO_ROOT / "scripts" / "ai" / "with-package-plane.sh").read_text(
        encoding="utf-8"
    )
    assert (
        "<ChummerUseLockedOwnerContractPackages "
        "Condition=\"'$(ChummerUseLockedOwnerContractPackages)' == ''\">"
        "false</ChummerUseLockedOwnerContractPackages>"
    ) in props
    assert "-p:ChummerUseLockedOwnerContractPackages=true" in helper
    assert "bootstrap-owner-contracts-feed.py" in helper
    assert "--print-version" in helper
    assert "--validate-only" in helper
    assert (
        'CHUMMER_ENGINE_CONTRACTS_PACKAGE_VERSION="$owner_contracts_package_version"'
        in helper
    )
    assert (
        "<ChummerLocalMediaContractsProject "
        "Condition=\"'$(ChummerLocalMediaContractsProject)' == ''\">"
        "$(ChummerCompatibilityRoot)fleet/repos/chummer-media-factory/"
        "src/Chummer.Media.Contracts/Chummer.Media.Contracts.csproj"
        "</ChummerLocalMediaContractsProject>"
    ) in props
    assert '-p:ChummerLocalMediaContractsProject="$media_contracts_project"' in helper
    assert 'media_contracts_project_dir="$(dirname "$media_contracts_project")"' in helper
    assert (
        '"$media_contracts_project_dir/obj/$prebuild_configuration/'
        'net10.0/ref/Chummer.Media.Contracts.dll"'
    ) in helper

    consumer_projects = (
        "Chummer.Presentation/Chummer.Presentation.csproj",
        "Chummer.Desktop.Runtime/Chummer.Desktop.Runtime.csproj",
        "Chummer.Avalonia/Chummer.Avalonia.csproj",
        "Chummer.Blazor/Chummer.Blazor.csproj",
        "Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj",
    )
    for relative_path in consumer_projects:
        root = ET.parse(REPO_ROOT / relative_path).getroot()
        local_run_conditions: list[str] = []
        locked_run_conditions: list[str] = []
        local_engine_references = 0
        for group in root.findall("ItemGroup"):
            group_condition = group.attrib.get("Condition", "")
            for reference in group:
                include = reference.attrib.get("Include")
                effective_condition = " ".join(
                    (group_condition, reference.attrib.get("Condition", ""))
                )
                if reference.tag == "ProjectReference" and include == "$(ChummerLocalContractsProject)":
                    local_engine_references += 1
                if reference.tag == "ProjectReference" and include == "$(ChummerLocalRunContractsProject)":
                    local_run_conditions.append(effective_condition)
                if reference.tag == "PackageReference" and include == "$(ChummerRunContractsPackageId)":
                    if "ChummerUseLockedOwnerContractPackages" in effective_condition:
                        locked_run_conditions.append(effective_condition)

        assert local_engine_references == 1, relative_path
        assert len(local_run_conditions) == 1, relative_path
        assert all(
            "'$(ChummerUseLockedOwnerContractPackages)' != 'true'" in condition
            for condition in local_run_conditions
        ), relative_path
        assert len(locked_run_conditions) == 1, relative_path
        assert all(
            "'$(ChummerUseLocalCompatibilityTree)' == 'true'" in condition
            and "'$(ChummerUseLockedOwnerContractPackages)' == 'true'" in condition
            for condition in locked_run_conditions
        ), relative_path

    desktop_root = ET.parse(
        REPO_ROOT / "Chummer.Desktop.Runtime" / "Chummer.Desktop.Runtime.csproj"
    ).getroot()
    local_registry_conditions: list[str] = []
    locked_registry_conditions: list[str] = []
    for group in desktop_root.findall("ItemGroup"):
        group_condition = group.attrib.get("Condition", "")
        for reference in group:
            effective_condition = " ".join(
                (group_condition, reference.attrib.get("Condition", ""))
            )
            if (
                reference.tag == "ProjectReference"
                and reference.attrib.get("Include") == "$(ChummerLocalHubRegistryContractsProject)"
            ):
                local_registry_conditions.append(effective_condition)
            if (
                reference.tag == "PackageReference"
                and reference.attrib.get("Include") == "$(ChummerHubRegistryContractsPackageId)"
                and "ChummerUseLockedOwnerContractPackages" in effective_condition
            ):
                locked_registry_conditions.append(effective_condition)
    assert len(local_registry_conditions) == 1
    assert "'$(ChummerUseLockedOwnerContractPackages)' != 'true'" in local_registry_conditions[0]
    assert len(locked_registry_conditions) == 1
    assert "'$(ChummerUseLockedOwnerContractPackages)' == 'true'" in locked_registry_conditions[0]

    presentation_root = ET.parse(
        REPO_ROOT / "Chummer.Presentation" / "Chummer.Presentation.csproj"
    ).getroot()
    media_references = [
        reference
        for group in presentation_root.findall("ItemGroup")
        for reference in group
        if reference.tag == "ProjectReference"
        and reference.attrib.get("Include") == "$(ChummerLocalMediaContractsProject)"
    ]
    assert len(media_references) == 1
    assert (
        media_references[0].attrib.get("Condition")
        == "Exists('$(ChummerLocalMediaContractsProject)')"
    )


def _write_restore_project(
    path: Path,
    body: str = "",
    properties: str = "",
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
        "  <PropertyGroup>\n"
        "    <TargetFramework>net10.0</TargetFramework>\n"
        "    <Version>0.0.0-local</Version>\n"
        f"{properties}"
        "  </PropertyGroup>\n"
        f"{body}"
        "</Project>\n",
        encoding="utf-8",
    )


def _write_owner_contract_package(
    feed: Path,
    package_id: str,
    version: str,
    dependencies: tuple[str, ...] = (),
) -> dict[str, object]:
    dependency_rows = "".join(
        f'        <dependency id="{dependency}" version="[{version}]" />\n'
        for dependency in dependencies
    )
    package_path = feed / f"{package_id}.{version}.nupkg"
    nuspec = (
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        "<package>\n"
        "  <metadata>\n"
        f"    <id>{package_id}</id>\n"
        f"    <version>{version}</version>\n"
        "    <authors>test</authors>\n"
        "    <description>Owner-contract restore fixture.</description>\n"
        "    <dependencies>\n"
        "      <group targetFramework=\"net10.0\">\n"
        f"{dependency_rows}"
        "      </group>\n"
        "    </dependencies>\n"
        "  </metadata>\n"
        "</package>\n"
    )
    with zipfile.ZipFile(package_path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec)
        archive.writestr(f"lib/net10.0/{package_id}.dll", b"restore-fixture")
    return {
        "id": package_id,
        "version": version,
        "file_name": package_path.name,
        "sha256": hashlib.sha256(package_path.read_bytes()).hexdigest(),
    }


def test_local_locked_owner_restore_derives_version_and_uses_one_source(
    tmp_path: Path,
) -> None:
    dotnet = shutil.which("dotnet")
    assert dotnet is not None
    owner_version = "0.0.0-packageplane.20260721.1"
    owners_root = tmp_path / "owners"
    core_root = owners_root / "core"
    hub_root = owners_root / "hub"
    registry_root = owners_root / "registry"
    ui_kit_root = owners_root / "ui-kit"
    media_root = owners_root / "media"
    contracts_project = core_root / "Chummer.Contracts" / "Chummer.Contracts.csproj"
    campaign_project = (
        hub_root / "Chummer.Campaign.Contracts" / "Chummer.Campaign.Contracts.csproj"
    )
    play_project = hub_root / "Chummer.Play.Contracts" / "Chummer.Play.Contracts.csproj"
    run_project = hub_root / "Chummer.Run.Contracts" / "Chummer.Run.Contracts.csproj"
    registry_project = (
        registry_root
        / "Chummer.Hub.Registry.Contracts"
        / "Chummer.Hub.Registry.Contracts.csproj"
    )
    ui_kit_project = ui_kit_root / "src" / "Chummer.Ui.Kit" / "Chummer.Ui.Kit.csproj"
    media_project = (
        media_root
        / "src"
        / "Chummer.Media.Contracts"
        / "Chummer.Media.Contracts.csproj"
    )
    _write_restore_project(
        contracts_project,
        properties=(
            "    <PackageId>Chummer.Engine.Contracts</PackageId>\n"
            "    <AssemblyName>Chummer.Engine.Contracts</AssemblyName>\n"
        ),
    )
    for project in (
        campaign_project,
        play_project,
        run_project,
        registry_project,
        ui_kit_project,
        media_project,
    ):
        _write_restore_project(project)

    feed = tmp_path / "owner-feed"
    feed.mkdir()
    package_rows = [
        _write_owner_contract_package(feed, "Chummer.Engine.Contracts", owner_version),
        _write_owner_contract_package(
            feed, "Chummer.Hub.Registry.Contracts", owner_version
        ),
        _write_owner_contract_package(feed, "Chummer.Play.Contracts", owner_version),
        _write_owner_contract_package(
            feed,
            "Chummer.Run.Contracts",
            owner_version,
            (
                "Chummer.Engine.Contracts",
                "Chummer.Hub.Registry.Contracts",
                "Chummer.Play.Contracts",
            ),
        ),
    ]
    inventory = {
        "contract": "chummer-core.owner-contract-package-inventory/v1",
        "package_plane_lock_sha256": "0" * 64,
        "package_version": owner_version,
        "packages": package_rows,
    }
    inventory_path = feed / "chummer-owner-contracts.inventory.json"
    inventory_path.write_text(json.dumps(inventory, indent=2) + "\n", encoding="utf-8")

    validation_marker = tmp_path / "validation.marker"
    owner_helper = core_root / "scripts" / "ai" / "bootstrap-owner-contracts-feed.py"
    owner_helper.parent.mkdir(parents=True)
    owner_helper.write_text(
        textwrap.dedent(
            """\
            #!/usr/bin/env python3
            import argparse
            import hashlib
            import json
            import os
            from pathlib import Path

            parser = argparse.ArgumentParser()
            parser.add_argument("--repo-root", required=True)
            parser.add_argument("--feed")
            parser.add_argument("--print-version", action="store_true")
            parser.add_argument("--validate-only", action="store_true")
            args = parser.parse_args()
            version = os.environ["EXPECTED_OWNER_CONTRACTS_VERSION"]
            if args.print_version:
                print(version)
                raise SystemExit(0)
            if not args.validate_only or not args.feed:
                raise SystemExit("expected --validate-only with an exact feed")
            feed = Path(args.feed).resolve()
            inventory_path = feed / "chummer-owner-contracts.inventory.json"
            payload = json.loads(inventory_path.read_text(encoding="utf-8"))
            expected_ids = (
                "Chummer.Engine.Contracts",
                "Chummer.Hub.Registry.Contracts",
                "Chummer.Play.Contracts",
                "Chummer.Run.Contracts",
            )
            if payload.get("contract") != "chummer-core.owner-contract-package-inventory/v1":
                raise SystemExit("inventory contract mismatch")
            if payload.get("package_version") != version:
                raise SystemExit("inventory version mismatch")
            rows = payload.get("packages")
            if not isinstance(rows, list) or tuple(row.get("id") for row in rows) != expected_ids:
                raise SystemExit("inventory package set mismatch")
            expected_files = {inventory_path.name}
            for row in rows:
                if row.get("version") != version:
                    raise SystemExit("inventory package version mismatch")
                package = feed / row["file_name"]
                expected_files.add(package.name)
                if hashlib.sha256(package.read_bytes()).hexdigest() != row.get("sha256"):
                    raise SystemExit("inventory package digest mismatch")
            if {path.name for path in feed.iterdir()} != expected_files:
                raise SystemExit("feed contains missing or unexpected entries")
            Path(os.environ["OWNER_VALIDATION_MARKER"]).write_text(version, encoding="utf-8")
            """
        ),
        encoding="utf-8",
    )

    bootstrap_marker = tmp_path / "bootstrap.marker"
    engine_bootstrap = tmp_path / "bootstrap-contracts-feed.sh"
    engine_bootstrap.write_text(
        "#!/usr/bin/env bash\n"
        "set -euo pipefail\n"
        'test "$CHUMMER_ENGINE_CONTRACTS_PACKAGE_VERSION" = '
        '"$EXPECTED_OWNER_CONTRACTS_VERSION"\n'
        'printf "%s" "$CHUMMER_ENGINE_CONTRACTS_PACKAGE_VERSION" > '
        '"$OWNER_BOOTSTRAP_MARKER"\n',
        encoding="utf-8",
    )
    engine_bootstrap.chmod(0o700)

    consumer = tmp_path / "consumer" / "OwnerGraph.Consumer.csproj"
    _write_restore_project(
        consumer,
        (
            "  <ItemGroup Condition=\"'$(ChummerUseLocalCompatibilityTree)' == 'true'\">\n"
            f"    <ProjectReference Include=\"{contracts_project.as_posix()}\" />\n"
            f"    <ProjectReference Include=\"{run_project.as_posix()}\" "
            "Condition=\"'$(ChummerUseLockedOwnerContractPackages)' != 'true'\" />\n"
            f"    <ProjectReference Include=\"{registry_project.as_posix()}\" "
            "Condition=\"'$(ChummerUseLockedOwnerContractPackages)' != 'true'\" />\n"
            "    <ProjectReference Include=\"$(ChummerLocalMediaContractsProject)\" "
            "Condition=\"Exists('$(ChummerLocalMediaContractsProject)')\" />\n"
            "  </ItemGroup>\n"
            "  <ItemGroup Condition=\"'$(ChummerUseLocalCompatibilityTree)' == 'true' "
            "and '$(ChummerUseLockedOwnerContractPackages)' == 'true'\">\n"
            "    <PackageReference Include=\"Chummer.Run.Contracts\" "
            "Version=\"$(ChummerRunContractsPackageVersion)\" />\n"
            "    <PackageReference Include=\"Chummer.Hub.Registry.Contracts\" "
            "Version=\"$(ChummerHubRegistryContractsPackageVersion)\" />\n"
            "  </ItemGroup>\n"
        ),
    )
    nuget_config = tmp_path / "NuGet.Config"
    nuget_config.write_text(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        "<configuration><packageSources><clear /></packageSources></configuration>\n",
        encoding="utf-8",
    )

    environment = os.environ.copy()
    for name in tuple(environment):
        if name.startswith("CHUMMER_") or name in {
            "NUGET_PACKAGES",
            "RestoreSources",
            "RestoreAdditionalProjectSources",
            "RestoreConfigFile",
        }:
            environment.pop(name, None)
    environment.update(
        {
            "CHUMMER_VERIFY_MODE": "slice",
            "CHUMMER_USE_LOCAL_COMPATIBILITY_TREE": "1",
            "CHUMMER_PACKAGE_PLANE_SERIALIZE": "0",
            "CHUMMER_LOCAL_CONTRACTS_PROJECT": str(contracts_project),
            "CHUMMER_LOCAL_CAMPAIGN_CONTRACTS_PROJECT": str(campaign_project),
            "CHUMMER_LOCAL_PLAY_CONTRACTS_PROJECT": str(play_project),
            "CHUMMER_LOCAL_RUN_CONTRACTS_PROJECT": str(run_project),
            "CHUMMER_LOCAL_HUB_REGISTRY_CONTRACTS_PROJECT": str(registry_project),
            "CHUMMER_LOCAL_UI_KIT_PROJECT": str(ui_kit_project),
            "CHUMMER_LOCAL_MEDIA_CONTRACTS_PROJECT": str(media_project),
            "CHUMMER_BOOTSTRAP_ENGINE_CONTRACTS_SCRIPT": str(engine_bootstrap),
            "CHUMMER_ENGINE_CONTRACTS_FEED": str(feed),
            "CHUMMER_PACKAGE_PLANE_LOCK_ROOT": str(tmp_path / "locks"),
            "NUGET_PACKAGES": str(tmp_path / "nuget-packages"),
            "DOTNET_CLI_HOME": str(tmp_path / "dotnet-home"),
            "EXPECTED_OWNER_CONTRACTS_VERSION": owner_version,
            "OWNER_BOOTSTRAP_MARKER": str(bootstrap_marker),
            "OWNER_VALIDATION_MARKER": str(validation_marker),
        }
    )
    command = [
        "bash",
        str(REPO_ROOT / "scripts" / "ai" / "with-package-plane.sh"),
        "restore",
        str(consumer),
        "--configfile",
        str(nuget_config),
        "--no-cache",
    ]
    completed = subprocess.run(
        command,
        cwd=REPO_ROOT,
        env=environment,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    assert completed.returncode == 0, completed.stdout
    assert bootstrap_marker.read_text(encoding="utf-8") == owner_version
    assert validation_marker.read_text(encoding="utf-8") == owner_version

    assets = json.loads(
        (consumer.parent / "obj" / "project.assets.json").read_text(encoding="utf-8")
    )
    libraries = assets["libraries"]
    expected_identities = {
        "Chummer.Engine.Contracts": "project",
        "Chummer.Hub.Registry.Contracts": "package",
        "Chummer.Play.Contracts": "package",
        "Chummer.Run.Contracts": "package",
        "Chummer.Media.Contracts": "project",
    }
    for package_id, expected_type in expected_identities.items():
        matches = [
            (identity, row)
            for identity, row in libraries.items()
            if identity.startswith(f"{package_id}/")
        ]
        assert len(matches) == 1, (package_id, matches)
        identity, row = matches[0]
        assert row["type"] == expected_type, identity
        if expected_type == "package":
            assert identity == f"{package_id}/{owner_version}"
    assert set(assets["project"]["restore"]["sources"]) == {str(feed.resolve())}
    assert not assets.get("logs")

    missing_media_project = tmp_path / "missing-media" / "Chummer.Media.Contracts.csproj"
    missing_media_environment = dict(environment)
    missing_media_environment["CHUMMER_LOCAL_MEDIA_CONTRACTS_PROJECT"] = str(
        missing_media_project
    )
    missing_media = subprocess.run(
        command,
        cwd=REPO_ROOT,
        env=missing_media_environment,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    assert missing_media.returncode == 2
    assert str(missing_media_project) in missing_media.stdout
    assert (
        "explicit local compatibility-tree mode is incomplete; "
        "every owner project must exist."
    ) in missing_media.stdout

    conflict_environment = dict(environment)
    conflict_environment["CHUMMER_RUN_CONTRACTS_PACKAGE_VERSION"] = "0.1.0-preview"
    conflict = subprocess.run(
        command,
        cwd=REPO_ROOT,
        env=conflict_environment,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    assert conflict.returncode == 2
    assert (
        "CHUMMER_RUN_CONTRACTS_PACKAGE_VERSION must equal the exact Core "
        f"owner-contract package version {owner_version}."
    ) in conflict.stdout

    inventory["packages"][0]["sha256"] = "f" * 64
    inventory_path.write_text(json.dumps(inventory, indent=2) + "\n", encoding="utf-8")
    invalid_environment = dict(environment)
    invalid_environment["CHUMMER_BOOTSTRAP_ENGINE_CONTRACTS_FEED"] = "0"
    invalid_inventory = subprocess.run(
        command,
        cwd=REPO_ROOT,
        env=invalid_environment,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    assert invalid_inventory.returncode == 2
    assert "Core owner-contract package inventory validation failed." in invalid_inventory.stdout


def test_private_sdk_and_every_execution_are_bound_to_exact_program_version() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    assert "sdk_root, sdk_archive_sha512 = acquire_sdk(" in source
    assert "owner_sdk_versions" in source
    assert '"sdkArchiveSha512": sdk_archive_sha512' in source
    assert '"buildExecutions": build_executions' in source
    assert '"testExecutions": test_executions' in source
    assert '"contractVersion": 11' in source
    assert "command = [\n        str(TRUSTED_PYTHON3)," in source
    assert "sys.executable" not in source
    assert '"canonicalOwnerFeed": canonical_feed_receipts["canonicalOwnerFeed"]' in source
    assert '"ownerPackageArtifactCache": owner_package_cache_receipt' in source
    assert '"projectLockFilesEnforced": True' in source


def test_sdk_archive_authority_is_exact() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["sdkArchive"]["sha512"] = "f" * 128
    with pytest.raises(package_plane.VerificationError, match="SDK version differs"):
        package_plane.validate_lock(lock)


def test_extra_package_or_external_source_row_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["packages"].append(dict(lock["packages"][0]))
    with pytest.raises(package_plane.VerificationError, match="cardinality"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["externalPackages"][0]["sha256"] = "f" * 64
    with pytest.raises(package_plane.VerificationError, match="fixed package/source set"):
        package_plane.validate_lock(lock)


def test_changed_consumer_source_is_rejected(tmp_path: Path) -> None:
    source = tmp_path / "Directory.Build.props"
    source.write_text("trusted\n", encoding="utf-8")
    locked = {"Directory.Build.props": package_plane.source_digest(source)}
    source.write_text("tampered\n", encoding="utf-8")
    with pytest.raises(package_plane.VerificationError, match="differs from package-plane lock"):
        package_plane.verify_source_files(tmp_path, locked)


def write_package(path: Path, content: bytes) -> None:
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("Package.nuspec", "<package />")
        archive.writestr("lib/net10.0/Package.dll", content)


def test_tampered_nupkg_reuse_changes_cryptographic_inventory(tmp_path: Path) -> None:
    package = tmp_path / "Package.1.0.0.nupkg"
    write_package(package, b"original")
    before = package_plane.package_inventory(tmp_path, {package.name})
    write_package(package, b"forged")
    after = package_plane.package_inventory(tmp_path, {package.name})
    with pytest.raises(package_plane.VerificationError, match="changed during restore/build"):
        package_plane.require_inventory_unchanged(before, after)


def test_locked_external_package_is_rehashed_at_final_inventory(tmp_path: Path) -> None:
    package = tmp_path / "Package.1.0.0.nupkg"
    write_package(package, b"original")
    locked = {package.name: hashlib.sha256(package.read_bytes()).hexdigest()}
    write_package(package, b"substituted")
    with pytest.raises(package_plane.VerificationError, match="locked package changed"):
        package_plane.package_inventory(tmp_path, {package.name}, locked)


def test_nested_or_linked_feed_entries_are_rejected(tmp_path: Path) -> None:
    package = tmp_path / "Package.1.0.0.nupkg"
    write_package(package, b"package")
    nested = tmp_path / "nested"
    nested.mkdir()
    with pytest.raises(package_plane.VerificationError, match="directory, link, or special"):
        package_plane.package_inventory(tmp_path, {package.name})
    nested.rmdir()
    target = tmp_path.parent / f"{tmp_path.name}-outside"
    target.mkdir()
    nested.symlink_to(target, target_is_directory=True)
    with pytest.raises(package_plane.VerificationError, match="directory, link, or special"):
        package_plane.package_inventory(tmp_path, {package.name})


def test_unexpected_feed_package_is_rejected(tmp_path: Path) -> None:
    write_package(tmp_path / "Expected.1.0.0.nupkg", b"expected")
    write_package(tmp_path / "Ambient.9.9.9.nupkg", b"ambient")
    with pytest.raises(package_plane.VerificationError, match="missing or unexpected"):
        package_plane.package_inventory(tmp_path, {"Expected.1.0.0.nupkg"})


def test_windows_runtime_closure_rows_sizes_authority_and_counts_are_exact() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    external = lock["externalPackages"]
    expected_rows = [
        {key: value for key, value in row.items() if key != "sizeBytes"}
        for row in package_plane.EXPECTED_WINDOWS_RUNTIME_PACKAGES
    ]
    locked_by_name = {row["fileName"]: row for row in external}

    assert [locked_by_name[row["fileName"]] for row in expected_rows] == expected_rows
    assert [row["sizeBytes"] for row in package_plane.EXPECTED_WINDOWS_RUNTIME_PACKAGES] == [
        40074136,
        12795776,
        5781842,
    ]
    assert len(external) == 87
    assert (
        len(external)
        + len(lock["currentOwnerContractFeed"]["packages"])
        + len(lock["canonicalOwnerFeed"]["packages"])
        + len(lock["coreRuntimeFeed"]["packages"])
        + len(lock["packages"])
        == 105
    )
    authority = hashlib.sha256(
        json.dumps(external, sort_keys=True, separators=(",", ":")).encode("utf-8")
    ).hexdigest()
    assert authority == "cd1054a9eeb9e36cbb5223c91d1e259746c848a41bc55c98fab1da5d355422a7"


def test_windows_runtime_download_requires_the_fixed_official_size(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    package = {
        key: value
        for key, value in package_plane.EXPECTED_WINDOWS_RUNTIME_PACKAGES[0].items()
        if key != "sizeBytes"
    }
    package["sha256"] = hashlib.sha256(b"x").hexdigest()
    monkeypatch.setattr(
        package_plane.urllib.request,
        "urlopen",
        lambda *_args, **_kwargs: io.BytesIO(b"x"),
    )

    with pytest.raises(package_plane.VerificationError, match="fixed size differs"):
        package_plane.acquire_external_package(package, tmp_path)
    assert not (tmp_path / package["fileName"]).exists()


def test_retained_bundle_cli_and_path_safety(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    target = tmp_path / "windows-bundle"
    receipt = tmp_path / "receipt.json"
    monkeypatch.setattr(
        package_plane.sys,
        "argv",
        [
            str(SCRIPT),
            "--receipt-output",
            str(receipt),
            "--retain-windows-bundle-output",
            str(target),
            "--windows-release-version",
            "preview-20260722.1",
            "--windows-release-channel",
            "preview",
        ],
    )
    args = package_plane.parse_args()
    assert args.retain_windows_bundle_output == target
    assert args.windows_release_version == "preview-20260722.1"
    assert args.windows_release_channel == "preview"

    with pytest.raises(package_plane.VerificationError, match="absolute"):
        package_plane.validate_retained_bundle_target(Path("relative-output"))

    target.mkdir()
    with pytest.raises(package_plane.VerificationError, match="must be absent"):
        package_plane.validate_retained_bundle_target(target)
    target.rmdir()

    dangling = tmp_path / "dangling-output"
    dangling.symlink_to(tmp_path / "missing")
    with pytest.raises(package_plane.VerificationError, match="must be absent"):
        package_plane.validate_retained_bundle_target(dangling)

    linked_parent = tmp_path / "linked-parent"
    linked_parent.symlink_to(tmp_path, target_is_directory=True)
    with pytest.raises(package_plane.VerificationError, match="physical"):
        package_plane.validate_retained_bundle_target(linked_parent / "output")

    writable_parent = tmp_path / "writable-parent"
    writable_parent.mkdir()
    writable_parent.chmod(0o777)
    with pytest.raises(package_plane.VerificationError, match="group/world-writable"):
        package_plane.validate_retained_bundle_target(writable_parent / "output")
    writable_parent.chmod(0o700)

    staging = tmp_path / "stage"
    staging.mkdir()
    staging.chmod(0o700)
    device = staging.stat().st_dev
    with pytest.raises(package_plane.VerificationError, match="cross-filesystem"):
        package_plane.require_same_filesystem(device + 1, staging)


def _retained_bundle_inputs(tmp_path: Path) -> dict[str, object]:
    feed = tmp_path / "feed"
    feed.mkdir()
    package = feed / "Package.1.0.0.nupkg"
    write_package(package, b"locked")
    locked = {package.name: hashlib.sha256(package.read_bytes()).hexdigest()}
    before = package_plane.package_inventory(feed, {package.name}, locked)
    config = tmp_path / "NuGet.Config"
    config.write_text("<configuration />\n", encoding="utf-8")
    lock_path = tmp_path / "package-plane.lock.json"
    lock_path.write_text("{}\n", encoding="utf-8")
    lock_inventory = package_plane.secure_regular_file_inventory(
        lock_path,
        label="test consumer lock",
        receipt_path=package_plane.CANONICAL_PACKAGE_PLANE_LOCK.as_posix(),
    )
    consumer = tmp_path / "consumer"
    project = consumer / package_plane.WINDOWS_PUBLISH_PROJECT
    project.parent.mkdir(parents=True)
    project.write_text("<Project />\n", encoding="utf-8")
    return {
        "consumer": consumer,
        "consumer_commit": "a" * 40,
        "consumer_config": config,
        "consumer_lock_inventory": lock_inventory,
        "environment": {"PATH": f"{tmp_path}/trusted-dotnet:/usr/bin:/bin"},
        "expected_feed_inventory": before,
        "expected_names": {package.name},
        "feed": feed,
        "locked_package_sha256": locked,
        "release_version": "preview-20260722.1",
        "release_channel": "preview",
    }


def _write_complete_windows_publish(output: Path) -> dict[str, bytes]:
    assets = {
        "Chummer.Avalonia.deps.json": b"deps",
        "Chummer.Avalonia.dll": b"managed",
        "Chummer.Avalonia.exe": b"native-host",
        "Chummer.Avalonia.runtimeconfig.json": b"runtime",
        "exact-same-run-byte.dat": b"do-not-repack",
    }
    for name, content in assets.items():
        (output / name).write_bytes(content)
    return assets


def test_windows_publish_closure_is_atomically_retained_with_exact_same_run_bytes(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    inputs = _retained_bundle_inputs(tmp_path)
    target = tmp_path / "retained-&-quote-\"'-less-<"
    captured: dict[str, object] = {}

    def fake_run(
        command: list[str],
        *,
        cwd: Path,
        environment: dict[str, str],
        capture: bool = False,
    ) -> subprocess.CompletedProcess[str]:
        captured["command"] = command
        assert cwd == inputs["consumer"]
        assert environment == inputs["environment"]
        assert capture is False
        output = Path(command[command.index("--output") + 1])
        captured["assets"] = _write_complete_windows_publish(output)
        return subprocess.CompletedProcess(command, 0)

    monkeypatch.setattr(package_plane, "run", fake_run)
    monkeypatch.setattr(
        package_plane,
        "require_clean_consumer_head",
        lambda *_args, **_kwargs: None,
    )
    receipt = package_plane.publish_and_retain_windows_bundle(
        target,
        **inputs,
    )

    command = captured["command"]
    assert isinstance(command, list)
    assert command[0:4] == [
        str(package_plane.TRUSTED_BASH),
        "scripts/ai/with-package-plane.sh",
        "publish",
        package_plane.WINDOWS_PUBLISH_PROJECT,
    ]
    assert command[command.index("-f") + 1] == "net10.0"
    assert command[command.index("-r") + 1] == "win-x64"
    assert command[command.index("--self-contained") + 1] == "true"
    assert "-p:ChummerDesktopReleaseVersion=preview-20260722.1" in command
    assert "-p:ChummerDesktopReleaseChannel=preview" in command
    assert receipt["atomicallyRetained"] is True
    assert receipt["authority"] is False
    assert receipt["consumerCommit"] == "a" * 40
    assert receipt["contractVersion"] == 2
    assert receipt["release"] == {
        "channel": "preview",
        "version": "preview-20260722.1",
    }
    assert receipt["targetPath"] == str(target)
    assert receipt["manifestIsAuthoritative"] is True
    manifest = json.loads((target / "manifest.json").read_text(encoding="utf-8"))
    assert manifest["contractVersion"] == 2
    assert manifest["feedInventory"]["beforePublishSha256"] == manifest["feedInventory"]["afterPublishSha256"]
    assert manifest["feedInventory"]["afterPublishSha256"] == manifest["feedInventory"]["retainedSha256"]
    assert manifest["assetInventory"]["afterPublishSha256"] == manifest["assetInventory"]["retainedSha256"]
    assert manifest["publish"]["status"] == "passed"
    assert manifest["publish"]["shell"] is False
    assert manifest["releaseEligibility"]["eligible"] is False
    assert manifest["deterministicRepacking"] is False
    assert manifest["release"] == {
        "channel": "preview",
        "version": "preview-20260722.1",
    }
    assert manifest["publish"]["releaseChannel"] == "preview"
    assert manifest["publish"]["releaseVersion"] == "preview-20260722.1"
    assert manifest["retainedNugetConfig"]["usableAtRetainedTarget"] is True
    assert manifest["retainedNugetConfig"]["packageSource"] == str(target / "feed")
    package_plane.require_exact_nuget_config_source(
        target / "config" / "NuGet.Config",
        target / "feed",
    )
    assets = captured["assets"]
    assert isinstance(assets, dict)
    assert (target / "assets" / "exact-same-run-byte.dat").read_bytes() == assets["exact-same-run-byte.dat"]
    assert stat.S_IMODE((target / "assets" / "exact-same-run-byte.dat").stat().st_mode) == 0o600
    assert stat.S_IMODE((target / "feed" / "Package.1.0.0.nupkg").stat().st_mode) == 0o600
    assert not list(tmp_path.glob(".chummer-win-retain-*"))
    assert not list(tmp_path.glob("chummer-win-publish-*"))


@pytest.mark.parametrize(
    ("version", "channel", "message"),
    [
        (None, "preview", "version"),
        ("local", "preview", "placeholder"),
        ("preview/unsafe", "preview", "portable"),
        ("preview-20260722.1", None, "channel"),
        ("preview-20260722.1", "stable", "exactly preview"),
    ],
)
def test_windows_release_authority_is_exact(
    version: str | None,
    channel: str | None,
    message: str,
) -> None:
    with pytest.raises(package_plane.VerificationError, match=message):
        package_plane.require_windows_release_authority(version, channel)


def test_windows_release_authority_accepts_preview() -> None:
    assert package_plane.require_windows_release_authority(
        "preview-20260722.1",
        "preview",
    ) == ("preview-20260722.1", "preview")


@pytest.mark.parametrize(
    "failure",
    [
        "publish",
        "partial",
        "feed-tamper",
        "asset-link",
        "asset-hardlink",
        "empty-directory",
        "unreadable-directory",
        "windows-invalid",
        "windows-reserved",
        "windows-casefold",
    ],
)
def test_windows_publish_closure_failures_leave_no_target_or_staging(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    failure: str,
) -> None:
    inputs = _retained_bundle_inputs(tmp_path)
    target = tmp_path / "retained-windows"

    def fake_run(
        command: list[str],
        *,
        cwd: Path,
        environment: dict[str, str],
        capture: bool = False,
    ) -> subprocess.CompletedProcess[str]:
        output = Path(command[command.index("--output") + 1])
        if failure == "publish":
            raise package_plane.VerificationError("injected publish failure")
        if failure == "partial":
            (output / "Chummer.Avalonia.exe").write_bytes(b"partial")
        else:
            _write_complete_windows_publish(output)
        if failure == "feed-tamper":
            feed = inputs["feed"]
            assert isinstance(feed, Path)
            write_package(feed / "Package.1.0.0.nupkg", b"tampered")
        if failure == "asset-link":
            (output / "linked-asset").symlink_to(output / "Chummer.Avalonia.dll")
        if failure == "asset-hardlink":
            os.link(output / "Chummer.Avalonia.dll", output / "hardlinked-asset")
        if failure == "empty-directory":
            (output / "empty").mkdir()
        if failure == "unreadable-directory":
            unreadable = output / "unreadable"
            unreadable.mkdir()
            (unreadable / "secret.dll").write_bytes(b"secret")
            unreadable.chmod(0o000)
        if failure == "windows-invalid":
            (output / "bad:name.dll").write_bytes(b"invalid")
        if failure == "windows-reserved":
            (output / "CON.txt").write_bytes(b"reserved")
        if failure == "windows-casefold":
            (output / "Case.dll").write_bytes(b"one")
            (output / "case.DLL").write_bytes(b"two")
        return subprocess.CompletedProcess(command, 0)

    monkeypatch.setattr(package_plane, "run", fake_run)
    monkeypatch.setattr(
        package_plane,
        "require_clean_consumer_head",
        lambda *_args, **_kwargs: None,
    )
    with pytest.raises(package_plane.VerificationError):
        package_plane.publish_and_retain_windows_bundle(target, **inputs)
    assert not target.exists()
    assert not target.is_symlink()
    assert not list(tmp_path.glob(".chummer-win-retain-*"))
    assert not list(tmp_path.glob("chummer-win-publish-*"))


def test_atomic_retention_never_replaces_an_existing_target(tmp_path: Path) -> None:
    staging = tmp_path / "staging"
    target = tmp_path / "target"
    staging.mkdir()
    target.mkdir()
    marker = target / "owned"
    marker.write_text("preserve\n", encoding="utf-8")

    with pytest.raises(package_plane.VerificationError, match="appeared"):
        package_plane.atomic_rename_noreplace(staging, target)
    assert staging.is_dir()
    assert marker.read_text(encoding="utf-8") == "preserve\n"


def test_owned_staging_cleanup_does_not_mutate_an_external_hardlink_inode(
    tmp_path: Path,
) -> None:
    external = tmp_path / "external.bin"
    external.write_bytes(b"external-authority")
    external.chmod(0o644)
    original = external.lstat()
    staging = tmp_path / "owned-staging"
    staging.mkdir(mode=0o700)
    staging_metadata = staging.lstat()
    os.link(external, staging / "linked.bin")
    assert external.lstat().st_nlink == 2

    package_plane.remove_owned_staging_tree(
        staging,
        (staging_metadata.st_dev, staging_metadata.st_ino),
    )

    final = external.lstat()
    assert external.read_bytes() == b"external-authority"
    assert stat.S_IMODE(final.st_mode) == stat.S_IMODE(original.st_mode) == 0o644
    assert (final.st_dev, final.st_ino) == (original.st_dev, original.st_ino)
    assert final.st_nlink == original.st_nlink == 1
    assert not staging.exists()


def test_outer_receipt_failure_rolls_back_the_exact_retained_target(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    target = tmp_path / "retained"
    target.mkdir()
    (target / "manifest.json").write_text("{}\n", encoding="utf-8")
    metadata = target.lstat()
    args = package_plane.argparse.Namespace(
        receipt_output=tmp_path / "receipt.json",
        retain_windows_bundle_output=target,
        _retained_bundle_identity=(metadata.st_dev, metadata.st_ino),
    )
    monkeypatch.setattr(
        package_plane,
        "exact_write_receipt",
        lambda *_args, **_kwargs: (_ for _ in ()).throw(OSError("injected fsync failure")),
    )

    with pytest.raises(OSError, match="injected fsync failure"):
        package_plane.commit_verification_receipt(args, {"status": "passed"})
    assert not target.exists()
    assert args._retained_bundle_identity is None
    assert not list(tmp_path.glob(".chummer-win-rollback-*"))


def test_main_rolls_back_retention_and_owned_temporary_on_context_exit_failure(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    target = tmp_path / "retained"
    target.mkdir()
    (target / "manifest.json").write_text("{}\n", encoding="utf-8")
    target_metadata = target.lstat()
    temporary = tmp_path / "chummer-ui-fresh-package-plane-injected"
    temporary.mkdir()
    unreadable = temporary / "unreadable"
    unreadable.mkdir()
    (unreadable / "secret").write_bytes(b"secret")
    unreadable.chmod(0o000)
    temporary_metadata = temporary.lstat()
    args = package_plane.argparse.Namespace(
        current_owner_contract_feed=None,
        lock=LOCK,
        receipt_output=tmp_path / "receipt.json",
        repo_root=REPO_ROOT,
        retain_windows_bundle_output=target,
        windows_release_version="preview-20260722.1",
        windows_release_channel="preview",
    )

    def fail_during_context_exit(namespace: object) -> dict[str, object]:
        setattr(
            namespace,
            "_retained_bundle_identity",
            (target_metadata.st_dev, target_metadata.st_ino),
        )
        setattr(namespace, "_verification_temporary_path", temporary)
        setattr(
            namespace,
            "_verification_temporary_identity",
            (temporary_metadata.st_dev, temporary_metadata.st_ino),
        )
        raise OSError("injected TemporaryDirectory cleanup failure")

    monkeypatch.setattr(package_plane, "parse_args", lambda: args)
    monkeypatch.setattr(package_plane, "verify", fail_during_context_exit)

    assert package_plane.main() == 2
    assert not target.exists()
    assert not temporary.exists()
    assert not args.receipt_output.exists()


def _git(command: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [str(package_plane.TRUSTED_GIT), *command],
        cwd=cwd,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=True,
    )


def test_owner_cache_retention_rejects_mid_run_dirty_consumer(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    repo = tmp_path / "consumer"
    repo.mkdir()
    _git(["init", "--quiet"], repo)
    _git(["config", "user.email", "test@example.invalid"], repo)
    _git(["config", "user.name", "Test"], repo)
    marker = repo / "marker.txt"
    marker.write_text("clean\n", encoding="utf-8")
    _git(["add", marker.name], repo)
    _git(["commit", "--quiet", "-m", "clean"], repo)
    head = _git(["rev-parse", "HEAD"], repo).stdout.strip()

    staging = tmp_path / ".owner-cache-staging"
    staging.mkdir(mode=0o700)
    (staging / "owner-package-cache.json").write_text("{}\n", encoding="utf-8")
    staging_metadata = staging.lstat()
    output = tmp_path / "retained-owner-cache"
    final_inventory = package_plane.directory_asset_inventory(staging)

    def dirty_consumer_during_fsync(_staging: Path) -> None:
        marker.write_text("dirty\n", encoding="utf-8")

    monkeypatch.setattr(
        package_plane,
        "fsync_asset_tree",
        dirty_consumer_during_fsync,
    )
    with pytest.raises(
        package_plane.VerificationError,
        match="consumer commit or clean state changed",
    ):
        package_plane.retain_owner_package_cache_transaction(
            staging=staging,
            output=output,
            parent=tmp_path,
            parent_device=tmp_path.lstat().st_dev,
            staging_identity=(staging_metadata.st_dev, staging_metadata.st_ino),
            final_inventory=final_inventory,
            repo_root=repo,
            environment=os.environ.copy(),
            expected_commit=head,
        )

    assert staging.is_dir()
    assert not output.exists()


def test_consumer_head_capture_survives_branch_advance_and_rejects_lock_swap(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    source = tmp_path / "source"
    source.mkdir()
    _git(["init", "--quiet"], source)
    _git(["config", "user.email", "test@example.invalid"], source)
    _git(["config", "user.name", "Test"], source)
    lock = source / package_plane.CANONICAL_PACKAGE_PLANE_LOCK
    lock.parent.mkdir()
    lock.write_text('{"authority":"captured"}\n', encoding="utf-8")
    marker = source / "marker.txt"
    marker.write_text("one\n", encoding="utf-8")
    _git(["add", package_plane.CANONICAL_PACKAGE_PLANE_LOCK.as_posix(), marker.name], source)
    _git(["commit", "--quiet", "-m", "captured"], source)

    head, canonical, lock_bytes, captured_inventory = (
        package_plane.capture_consumer_authority(source, lock)
    )
    alternate = source / "alternate-lock.json"
    alternate.write_text("{}\n", encoding="utf-8")
    with pytest.raises(package_plane.VerificationError, match="canonical in-repo"):
        package_plane.capture_consumer_authority(source, alternate)
    alternate.unlink()
    assert canonical == lock

    marker.write_text("two\n", encoding="utf-8")
    _git(["add", marker.name], source)
    _git(["commit", "--quiet", "-m", "advanced"], source)
    assert _git(["rev-parse", "HEAD"], source).stdout.strip() != head

    consumer_parent = tmp_path / "consumers"
    consumer_parent.mkdir()
    consumer = consumer_parent / "exact"
    cloned_inventory = package_plane.clone_exact_consumer(
        source,
        consumer,
        consumer_parent,
        os.environ.copy(),
        head,
        lock_bytes,
    )
    assert cloned_inventory == captured_inventory
    assert _git(["rev-parse", "HEAD"], consumer).stdout.strip() == head

    swapped_consumer = consumer_parent / "swapped"

    def swap_lock(clone: Path, *_args: object, **_kwargs: object) -> None:
        (clone / package_plane.CANONICAL_PACKAGE_PLANE_LOCK).write_text(
            '{"authority":"swapped"}\n',
            encoding="utf-8",
        )

    monkeypatch.setattr(package_plane, "require_clean_consumer_head", swap_lock)
    with pytest.raises(package_plane.VerificationError, match="lock bytes differ"):
        package_plane.clone_exact_consumer(
            source,
            swapped_consumer,
            consumer_parent,
            os.environ.copy(),
            head,
            lock_bytes,
        )
