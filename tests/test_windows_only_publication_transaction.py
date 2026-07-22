from __future__ import annotations

import argparse
import fcntl
import hashlib
import importlib.util
import json
import os
import shutil
import signal
import stat
import subprocess
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


scope = load_module(
    "transaction_test_scope", ROOT / "scripts/preview_nightly_publication_scope.py"
)
transaction = load_module(
    "windows_only_publication_transaction",
    ROOT / "scripts/windows_only_publication_transaction.py",
)
scope_fixtures = load_module(
    "transaction_scope_fixtures",
    ROOT / "tests/test_preview_nightly_publication_scope.py",
)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def exact_evidence_fixture(tmp_path: Path) -> tuple[dict[str, object], Path, Path]:
    values, proposal = scope_fixtures.prepare(tmp_path)
    proposal_path = values["paths"]["output"]
    final_path = scope_fixtures.finalize_for_test(tmp_path, values, proposal)
    return values, proposal_path, final_path


def incumbent_shelf(tmp_path: Path, values: dict[str, object], name: str) -> Path:
    root = tmp_path / name
    shutil.copytree(values["incumbent_shelf"], root, copy_function=shutil.copy2)
    return root


def prepare_target(
    tmp_path: Path,
    evidence_root: Path,
    values: dict[str, object],
    proposal_path: Path,
    final_path: Path,
    incumbent: Path,
    suffix: str,
) -> tuple[Path, dict[str, object]]:
    prepared = tmp_path / f"prepared-{suffix}"
    receipt_path = tmp_path / f"generation-{suffix}.json"
    receipt = transaction.prepare_generation(
        argparse.Namespace(
            scope=final_path,
            proposal=proposal_path,
            evidence_root=evidence_root,
            publication_dir=values["paths"]["publication_dir"],
            incumbent=incumbent,
            output_dir=prepared,
            receipt=receipt_path,
        )
    )
    return prepared, receipt


def test_prepare_preserves_aur_and_arbitrary_incumbent_bytes(tmp_path: Path) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )

    for relative in (
        "aur-packages.json",
        "files/chummer6-bin-aur-source.tar.gz",
        "files/chummer6-bin.PKGBUILD",
        "files/chummer6-bin.SRCINFO",
        "operator-note.txt",
    ):
        assert (prepared / relative).read_bytes() == (incumbent / relative).read_bytes()
        assert stat.S_IMODE((prepared / relative).stat().st_mode) == stat.S_IMODE(
            (incumbent / relative).stat().st_mode
        )
    assert receipt["ancillaryInventorySha256"]
    new_windows = prepared / "files/chummer-avalonia-win-x64-installer.exe"
    assert digest(new_windows) == values["win_sha"]
    proposal = json.loads(proposal_path.read_text(encoding="utf-8"))
    assert scope.file_inventory(prepared) == proposal["fullShelfInventory"]
    assert all("uid" not in row and "gid" not in row for row in transaction.inventory_tree(prepared))


def test_prepare_binds_exact_authoritative_run_upload_inventory(tmp_path: Path) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )

    selected = []
    for path in sorted(item for item in prepared.rglob("*") if item.is_file()):
        relative = path.relative_to(prepared).as_posix()
        if relative in receipt["runUploadPaths"]:
            selected.append((relative, path.stat().st_size, digest(path)))
    inventory = hashlib.sha256()
    for relative, size_bytes, sha256 in selected:
        encoded = relative.encode("utf-8")
        inventory.update(len(encoded).to_bytes(8, "big"))
        inventory.update(encoded)
        inventory.update(size_bytes.to_bytes(8, "big"))
        inventory.update(bytes.fromhex(sha256))

    candidate = receipt["runUploadCandidate"]
    assert candidate["canonicalManifestSha256"] == digest(
        prepared / scope.CANONICAL_MANIFEST_NAME
    )
    assert candidate["inventorySha256"] == inventory.hexdigest()
    assert candidate["fileCount"] == len(selected)
    assert candidate["totalBytes"] == sum(row[1] for row in selected)
    assert "operator-note.txt" not in {row[0] for row in selected}
    assert "files/chummer6-bin-aur-source.tar.gz" not in receipt["runUploadPaths"]
    assert "files/chummer6-bin.PKGBUILD" not in receipt["runUploadPaths"]
    assert "files/chummer6-bin.SRCINFO" not in receipt["runUploadPaths"]


def test_prepare_durably_syncs_final_tree_bottom_up_before_generation_receipt(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared = tmp_path / "prepared-live"
    receipt_path = tmp_path / "generation-live.json"
    events: list[tuple[str, Path]] = []
    original_verify = transaction.verify_prepared_generation
    original_file_sync = transaction._fsync_regular_file
    original_directory_sync = transaction.fsync_directory
    original_write = transaction.write_new_json

    def tracked_verify(*args, **kwargs):
        result = original_verify(*args, **kwargs)
        events.append(("verified", Path(args[0])))
        return result

    def tracked_file_sync(path: Path, label: str) -> None:
        events.append(("file", Path(path)))
        original_file_sync(path, label)

    def tracked_directory_sync(path: Path) -> None:
        events.append(("directory", Path(path)))
        original_directory_sync(path)

    def tracked_write(path: Path, payload: dict[str, object]) -> None:
        events.append(("receipt-start", Path(path)))
        original_write(path, payload)
        events.append(("receipt-finished", Path(path)))

    monkeypatch.setattr(transaction, "verify_prepared_generation", tracked_verify)
    monkeypatch.setattr(transaction, "_fsync_regular_file", tracked_file_sync)
    monkeypatch.setattr(transaction, "fsync_directory", tracked_directory_sync)
    monkeypatch.setattr(transaction, "write_new_json", tracked_write)

    prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )

    verified = events.index(("verified", prepared))
    prepared_file_syncs = [
        index
        for index, event in enumerate(events)
        if event[0] == "file" and prepared in event[1].parents
    ]
    assert prepared_file_syncs and verified < min(prepared_file_syncs)
    files_directory = events.index(("directory", prepared / "files"))
    generation_directory = events.index(("directory", prepared))
    transaction_root = events.index(("directory", prepared.parent))
    transaction_root_parent = events.index(("directory", prepared.parent.parent))
    receipt_start = events.index(("receipt-start", receipt_path))
    receipt_finished = events.index(("receipt-finished", receipt_path))
    assert verified < files_directory < generation_directory
    assert generation_directory < transaction_root < transaction_root_parent
    assert transaction_root_parent < receipt_start < receipt_finished
    assert any(
        event == ("directory", receipt_path.parent)
        for event in events[receipt_start + 1 : receipt_finished]
    )


def test_prepare_directory_fsync_failure_cannot_emit_generation_receipt(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared = tmp_path / "prepared-live"
    receipt_path = tmp_path / "generation-live.json"
    original_directory_sync = transaction.fsync_directory
    failed = False

    def fail_prepared_child_sync(path: Path) -> None:
        nonlocal failed
        if Path(path) == prepared / "files" and not failed:
            failed = True
            raise OSError("injected prepared generation directory fsync failure")
        original_directory_sync(path)

    monkeypatch.setattr(transaction, "fsync_directory", fail_prepared_child_sync)
    with pytest.raises(OSError, match="prepared generation directory fsync failure"):
        prepare_target(
            tmp_path,
            values["evidence_root"],
            values,
            proposal_path,
            final_path,
            incumbent,
            "live",
        )

    assert failed is True
    assert not prepared.exists()
    assert not receipt_path.exists()


def test_run_upload_candidate_excludes_stale_incumbent_evidence_sidecars(
    tmp_path: Path,
) -> None:
    root = tmp_path / "shelf"
    for relative, content in (
        (scope.CANONICAL_MANIFEST_NAME, b"canonical"),
        (scope.COMPATIBILITY_MANIFEST_NAME, b"compatibility"),
        ("files/chummer-avalonia-win-x64-installer.exe", b"new windows"),
        ("proof/old-windows-proof.json", b"stale proof"),
        ("signing/old-windows-signing.json", b"stale signing"),
        ("startup-smoke/old-windows-smoke.json", b"stale smoke"),
        ("release-evidence/public-promotion.json", b"stale promotion"),
        ("aur-packages.json", b"retained ancillary"),
    ):
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)

    approved_paths = sorted(
        {
            *transaction.RUN_UPLOAD_ROOT_FILES,
            "files/chummer-avalonia-win-x64-installer.exe",
        }
    )
    candidate = transaction.run_upload_candidate(
        transaction.inventory_tree(root), "preview-test", approved_paths
    )
    approved = [
        row
        for row in transaction.inventory_tree(root)
        if row["type"] == "file" and row["path"] in approved_paths
    ]
    assert {row["path"] for row in approved} == {
        scope.CANONICAL_MANIFEST_NAME,
        scope.COMPATIBILITY_MANIFEST_NAME,
        "files/chummer-avalonia-win-x64-installer.exe",
    }
    assert candidate["fileCount"] == len(approved)
    assert candidate["totalBytes"] == sum(row["sizeBytes"] for row in approved)


@pytest.mark.parametrize(
    "invalid_path",
    [
        "files//chummer.exe",
        "files/./chummer.exe",
        "files\\chummer.exe",
        "files/CON.exe",
        "files/cafe\u0301.exe",
    ],
)
def test_run_upload_allowlist_rejects_noncanonical_or_nonportable_aliases(
    invalid_path: str,
) -> None:
    paths = sorted({*transaction.RUN_UPLOAD_ROOT_FILES, invalid_path})
    with pytest.raises(transaction.TransactionError):
        transaction.validate_run_upload_paths(paths)


def test_run_upload_allowlist_rejects_windows_casefold_collisions() -> None:
    paths = sorted(
        {
            *transaction.RUN_UPLOAD_ROOT_FILES,
            "files/Chummer.exe",
            "files/chummer.EXE",
        }
    )
    with pytest.raises(transaction.TransactionError, match="case-collide"):
        transaction.validate_run_upload_paths(paths)


def test_forged_valid_looking_final_scope_is_rejected_by_evidence_replay(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    forged_path = tmp_path / "forged-scope.json"
    forged = json.loads(final_path.read_text(encoding="utf-8"))
    forged["nativeEvidenceSha256"] = "f" * 64
    forged["nativeEvidenceComposite"]["wrapper"]["sha256"] = "f" * 64
    write_json(forged_path, forged)

    with pytest.raises(scope.ScopeError, match="native Windows evidence bytes changed"):
        scope.verify_scope(
            argparse.Namespace(
                scope=forged_path,
                proposal=proposal_path,
                publication_dir=values["paths"]["publication_dir"],
                evidence_root=values["evidence_root"],
            )
        )


def test_exchange_and_rollback_restore_exact_incumbent_tree(tmp_path: Path) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    before = transaction.inventory_tree(incumbent)
    prepared, receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )

    transaction.exchange(
        argparse.Namespace(
            left=incumbent,
            right=prepared,
            expected_left_inventory=receipt["incumbentInventorySha256"],
            expected_right_inventory=receipt["preparedInventorySha256"],
        )
    )
    assert transaction.canonical_sha256(transaction.inventory_tree(incumbent)) == receipt[
        "preparedInventorySha256"
    ]
    transaction.exchange(
        argparse.Namespace(
            left=incumbent,
            right=prepared,
            expected_left_inventory=receipt["preparedInventorySha256"],
            expected_right_inventory=receipt["incumbentInventorySha256"],
        )
    )
    assert transaction.inventory_tree(incumbent) == before


def test_exchange_endpoint_rejects_path_replacement_after_descriptor_hold(
    tmp_path: Path,
) -> None:
    endpoint_path = tmp_path / "endpoint"
    endpoint_path.mkdir()
    (endpoint_path / "bound.bin").write_bytes(b"bound")
    endpoint = transaction._open_exchange_endpoint(endpoint_path, "held endpoint")
    original_identity = endpoint["childIdentity"]
    displaced = tmp_path / "displaced"
    try:
        endpoint_path.rename(displaced)
        endpoint_path.mkdir()
        (endpoint_path / "replacement.bin").write_bytes(b"replacement")
        with pytest.raises(transaction.TransactionError, match="aliased"):
            transaction._validate_exchange_endpoint(endpoint, original_identity)
    finally:
        transaction._close_exchange_endpoint(endpoint)


def test_activation_receipt_binds_exact_generation_and_transaction(tmp_path: Path) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    generation_receipt = tmp_path / "generation-live.json"
    activation_receipt = tmp_path / "activation-live.json"

    activated = transaction.activate(
        argparse.Namespace(
            target=incumbent,
            prepared=prepared,
            generation_receipt=generation_receipt,
            transaction_id="windows-nightly-test-0001",
            receipt=activation_receipt,
        )
    )

    assert activated["status"] == "activated"
    assert activated["fullShelfInventorySha256"] == receipt[
        "fullShelfInventorySha256"
    ]
    assert activated["transactionId"] == "windows-nightly-test-0001"
    assert digest(activation_receipt)


def test_durable_prepared_record_recovers_child_exit_before_shell_bookkeeping(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    incumbent_before = transaction.inventory_tree(incumbent)
    prepared, receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    receipt_dir = tmp_path / "receipts"
    receipt_dir.mkdir()
    prepared_record = receipt_dir / "run.transaction.prepared.json"
    activation_journal = receipt_dir / "run.transaction.json"
    activation_receipt = tmp_path / "activation-live.json"
    rollback = receipt_dir / "run.transaction.rolled-back.json"
    transaction_id = "windows-nightly-prepared-0001"
    transaction.create_prepared_transaction(
        argparse.Namespace(
            transaction_id=transaction_id,
            generation_receipt=[tmp_path / "generation-live.json"],
            target=[incumbent],
            prepared=[prepared],
            activation_receipt=[activation_receipt],
            activation_journal=activation_journal,
            output=prepared_record,
        )
    )
    assert prepared_record.is_file()

    transaction.activate(
        argparse.Namespace(
            target=incumbent,
            prepared=prepared,
            generation_receipt=tmp_path / "generation-live.json",
            transaction_id=transaction_id,
            receipt=activation_receipt,
        )
    )
    assert transaction.canonical_sha256(transaction.inventory_tree(incumbent)) == receipt[
        "preparedInventorySha256"
    ]

    recovered = transaction.recover_prepared_transaction(
        argparse.Namespace(
            prepared_record=prepared_record,
            activation_journal=activation_journal,
            commit=receipt_dir / "run.transaction.committed.json",
            rollback=rollback,
        )
    )
    assert recovered["status"] == "rolled_back"
    assert transaction.inventory_tree(incumbent) == incumbent_before
    assert rollback.is_file()


def test_prepare_enrollment_waits_for_every_durable_input_and_recovers_deterministically(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, _receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    generation_receipt = tmp_path / "generation-live.json"
    activation_receipt = tmp_path / "activation-live.json"
    receipt_dir = tmp_path / "receipts"
    receipt_dir.mkdir()
    original_directory_sync = transaction.fsync_directory
    original_durable_write = transaction._write_new_bytes_durable
    failed = False

    def fail_receipt_directory_sync(path: Path) -> None:
        nonlocal failed
        if Path(path) == receipt_dir and not failed:
            failed = True
            raise OSError("injected PREPARE receipt-directory fsync failure")
        original_directory_sync(path)

    def prepared_args(prefix: str) -> argparse.Namespace:
        return argparse.Namespace(
            transaction_id=f"windows-nightly-durable-{prefix}-0001",
            generation_receipt=[generation_receipt],
            target=[incumbent],
            prepared=[prepared],
            activation_receipt=[activation_receipt],
            activation_journal=receipt_dir / f"{prefix}.transaction.json",
            output=receipt_dir / f"{prefix}.transaction.prepared.json",
        )

    failed_args = prepared_args("failed")
    monkeypatch.setattr(transaction, "fsync_directory", fail_receipt_directory_sync)
    with pytest.raises(OSError, match="PREPARE receipt-directory fsync failure"):
        transaction.create_prepared_transaction(failed_args)
    assert failed is True
    assert not failed_args.output.exists()
    monkeypatch.setattr(transaction, "fsync_directory", original_directory_sync)
    assert transaction.recover_discovered_transactions(
        argparse.Namespace(receipt_dir=receipt_dir)
    ) == {"reconciled": [], "status": "clean"}

    writing_prepared_record = False
    record_directory_sync_failed = False

    def tracked_prepared_record_write(path: Path, data: bytes, label: str) -> None:
        nonlocal writing_prepared_record
        writing_prepared_record = label == "prepared transaction record"
        try:
            original_durable_write(path, data, label)
        finally:
            writing_prepared_record = False

    def fail_record_directory_sync(path: Path) -> None:
        nonlocal record_directory_sync_failed
        if (
            writing_prepared_record
            and Path(path) == receipt_dir
            and not record_directory_sync_failed
        ):
            record_directory_sync_failed = True
            raise OSError("injected durable PREPARE record directory fsync failure")
        original_directory_sync(path)

    record_failed_args = prepared_args("record-failed")
    monkeypatch.setattr(
        transaction, "_write_new_bytes_durable", tracked_prepared_record_write
    )
    monkeypatch.setattr(transaction, "fsync_directory", fail_record_directory_sync)
    with pytest.raises(OSError, match="durable PREPARE record directory fsync failure"):
        transaction.create_prepared_transaction(record_failed_args)
    assert record_directory_sync_failed is True
    assert not record_failed_args.output.exists()
    monkeypatch.setattr(transaction, "_write_new_bytes_durable", original_durable_write)
    monkeypatch.setattr(transaction, "fsync_directory", original_directory_sync)
    assert transaction.recover_discovered_transactions(
        argparse.Namespace(receipt_dir=receipt_dir)
    ) == {"reconciled": [], "status": "clean"}

    events: list[tuple[str, Path | str]] = []
    original_tree_sync = transaction.fsync_tree_bottom_up
    original_file_sync = transaction._fsync_regular_file

    def tracked_tree_sync(path: Path) -> None:
        events.append(("tree", Path(path)))
        original_tree_sync(path)

    def tracked_file_sync(path: Path, label: str) -> None:
        events.append(("file", Path(path)))
        original_file_sync(path, label)

    def tracked_directory_sync(path: Path) -> None:
        events.append(("directory", Path(path)))
        original_directory_sync(path)

    def tracked_write(path: Path, data: bytes, label: str) -> None:
        events.append(("write", label))
        original_durable_write(path, data, label)

    monkeypatch.setattr(transaction, "fsync_tree_bottom_up", tracked_tree_sync)
    monkeypatch.setattr(transaction, "_fsync_regular_file", tracked_file_sync)
    monkeypatch.setattr(transaction, "fsync_directory", tracked_directory_sync)
    monkeypatch.setattr(transaction, "_write_new_bytes_durable", tracked_write)
    enrolled_args = prepared_args("run")
    transaction.create_prepared_transaction(enrolled_args)

    write_index = events.index(("write", "prepared transaction record"))
    for prerequisite in (
        ("tree", incumbent),
        ("tree", prepared),
        ("file", generation_receipt),
        ("directory", incumbent.parent),
        ("directory", prepared.parent),
        ("directory", prepared.parent.parent),
        ("directory", receipt_dir),
        ("directory", receipt_dir.parent),
    ):
        assert events.index(prerequisite) < write_index

    first_recovery = transaction.recover_discovered_transactions(
        argparse.Namespace(receipt_dir=receipt_dir)
    )
    second_recovery = transaction.recover_discovered_transactions(
        argparse.Namespace(receipt_dir=receipt_dir)
    )
    assert first_recovery["reconciled"] == [
        {
            "status": "rolled_back",
            "transactionId": "windows-nightly-durable-run-0001",
        }
    ]
    assert second_recovery == first_recovery


def test_durable_prepared_record_reconciles_every_target_after_partial_activation(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    first = incumbent_shelf(tmp_path, values, "first")
    second = incumbent_shelf(tmp_path, values, "second")
    first_before = transaction.inventory_tree(first)
    second_before = transaction.inventory_tree(second)
    first_prepared, _ = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        first,
        "first",
    )
    second_prepared, _ = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        second,
        "second",
    )
    receipts = tmp_path / "receipts"
    receipts.mkdir()
    prepared_record = receipts / "run.transaction.prepared.json"
    activation_journal = receipts / "run.transaction.json"
    first_activation = tmp_path / "activation-first.json"
    second_activation = tmp_path / "activation-second.json"
    transaction_id = "windows-nightly-prepared-0002"
    transaction.create_prepared_transaction(
        argparse.Namespace(
            transaction_id=transaction_id,
            generation_receipt=[
                tmp_path / "generation-first.json",
                tmp_path / "generation-second.json",
            ],
            target=[first, second],
            prepared=[first_prepared, second_prepared],
            activation_receipt=[first_activation, second_activation],
            activation_journal=activation_journal,
            output=prepared_record,
        )
    )
    transaction.activate(
        argparse.Namespace(
            target=first,
            prepared=first_prepared,
            generation_receipt=tmp_path / "generation-first.json",
            transaction_id=transaction_id,
            receipt=first_activation,
        )
    )

    transaction.recover_prepared_transaction(
        argparse.Namespace(
            prepared_record=prepared_record,
            activation_journal=activation_journal,
            commit=receipts / "run.transaction.committed.json",
            rollback=receipts / "run.transaction.rolled-back.json",
        )
    )
    assert transaction.inventory_tree(first) == first_before
    assert transaction.inventory_tree(second) == second_before


def test_ambiguous_prepared_recovery_preserves_record_and_generations(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, _ = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    receipts = tmp_path / "receipts"
    receipts.mkdir()
    prepared_record = receipts / "run.transaction.prepared.json"
    activation_journal = receipts / "run.transaction.json"
    transaction.create_prepared_transaction(
        argparse.Namespace(
            transaction_id="windows-nightly-prepared-0003",
            generation_receipt=[tmp_path / "generation-live.json"],
            target=[incumbent],
            prepared=[prepared],
            activation_receipt=[tmp_path / "activation-live.json"],
            activation_journal=activation_journal,
            output=prepared_record,
        )
    )
    (incumbent / "operator-note.txt").write_bytes(b"ambiguous external drift")

    with pytest.raises(transaction.TransactionError, match="unrecognized"):
        transaction.recover_prepared_transaction(
            argparse.Namespace(
                prepared_record=prepared_record,
                activation_journal=activation_journal,
                commit=receipts / "run.transaction.committed.json",
                rollback=receipts / "run.transaction.rolled-back.json",
            )
        )
    assert prepared_record.is_file()
    assert prepared.is_dir()
    assert not (receipts / "run.transaction.rolled-back.json").exists()


def test_fresh_invocation_discovers_and_recovers_transaction_after_sigkill(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    incumbent_before = transaction.inventory_tree(incumbent)
    prepared, _ = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    receipts = tmp_path / "receipts"
    receipts.mkdir()
    prepared_record = receipts / "run.transaction.prepared.json"
    activation_journal = receipts / "run.transaction.json"
    activation_receipt = tmp_path / "activation-live.json"
    transaction_id = "windows-nightly-sigkill-0001"
    transaction.create_prepared_transaction(
        argparse.Namespace(
            transaction_id=transaction_id,
            generation_receipt=[tmp_path / "generation-live.json"],
            target=[incumbent],
            prepared=[prepared],
            activation_receipt=[activation_receipt],
            activation_journal=activation_journal,
            output=prepared_record,
        )
    )

    killed = subprocess.run(
        [
            sys.executable,
            "-c",
            (
                "import os,signal,subprocess,sys; "
                "subprocess.run(sys.argv[1:], check=True); "
                "os.kill(os.getpid(), signal.SIGKILL)"
            ),
            sys.executable,
            str(ROOT / "scripts/windows_only_publication_transaction.py"),
            "activate",
            "--target",
            str(incumbent),
            "--prepared",
            str(prepared),
            "--generation-receipt",
            str(tmp_path / "generation-live.json"),
            "--transaction-id",
            transaction_id,
            "--receipt",
            str(activation_receipt),
        ],
        text=True,
        capture_output=True,
        check=False,
    )
    assert killed.returncode == -signal.SIGKILL

    restarted = subprocess.run(
        [
            sys.executable,
            str(ROOT / "scripts/windows_only_publication_transaction.py"),
            "recover-discovered",
            "--receipt-dir",
            str(receipts),
        ],
        text=True,
        capture_output=True,
        check=False,
    )
    assert restarted.returncode == 0, restarted.stderr
    assert json.loads(restarted.stdout) == {
        "reconciled": [
            {"status": "rolled_back", "transactionId": transaction_id}
        ],
        "status": "reconciled",
    }
    assert transaction.inventory_tree(incumbent) == incumbent_before
    assert (receipts / "run.transaction.rolled-back.json").is_file()


def test_discovery_preflights_ambiguity_and_preserves_every_durable_input(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, _ = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    receipts = tmp_path / "receipts"
    receipts.mkdir()
    prepared_record = receipts / "run.transaction.prepared.json"
    transaction.create_prepared_transaction(
        argparse.Namespace(
            transaction_id="windows-nightly-discovery-ambiguous-0001",
            generation_receipt=[tmp_path / "generation-live.json"],
            target=[incumbent],
            prepared=[prepared],
            activation_receipt=[tmp_path / "activation-live.json"],
            activation_journal=receipts / "run.transaction.json",
            output=prepared_record,
        )
    )
    (incumbent / "operator-note.txt").write_bytes(b"ambiguous-after-sigkill")
    prepared_before = transaction.inventory_tree(prepared)

    with pytest.raises(transaction.TransactionError, match="manual reconciliation"):
        transaction.recover_discovered_transactions(
            argparse.Namespace(receipt_dir=receipts)
        )

    assert prepared_record.is_file()
    assert transaction.inventory_tree(prepared) == prepared_before
    assert not (receipts / "run.transaction.rolled-back.json").exists()


def test_discovery_respects_an_active_durable_target_lock_lease(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, _ = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    receipts = tmp_path / "receipts"
    receipts.mkdir()
    prepared_record = receipts / "run.transaction.prepared.json"
    transaction.create_prepared_transaction(
        argparse.Namespace(
            transaction_id="windows-nightly-active-lock-0001",
            generation_receipt=[tmp_path / "generation-live.json"],
            target=[incumbent],
            prepared=[prepared],
            activation_receipt=[tmp_path / "activation-live.json"],
            activation_journal=receipts / "run.transaction.json",
            output=prepared_record,
        )
    )
    lock_dir = incumbent.parent / transaction.DISCOVERY_LOCK_DIRECTORY
    lock_dir.mkdir(mode=0o700)
    lease = lock_dir / transaction.DISCOVERY_LOCK_FILE
    descriptor = os.open(lease, os.O_RDWR | os.O_CREAT, 0o600)
    try:
        fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        with pytest.raises(transaction.TransactionError, match="active Windows-only"):
            transaction.recover_discovered_transactions(
                argparse.Namespace(receipt_dir=receipts)
            )
    finally:
        fcntl.flock(descriptor, fcntl.LOCK_UN)
        os.close(descriptor)
    assert prepared_record.is_file()
    assert not (receipts / "run.transaction.rolled-back.json").exists()


def test_publisher_reconciles_discovered_transactions_before_new_prepare() -> None:
    publisher = (ROOT / "scripts/publish-download-bundle.sh").read_text(
        encoding="utf-8"
    )
    call_block = publisher[publisher.index(
        'if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" == "true" ]]; then\n'
        "  initialize_windows_only_publication_transaction"
    ):]
    assert call_block.index(
        "initialize_windows_only_publication_transaction"
    ) < call_block.index(
        "reconcile_discovered_windows_only_publication_transactions"
    ) < call_block.index("prepare_windows_only_publication_targets")
    assert "recover-discovered" in publisher
    assert '--receipt-dir "$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR"' in publisher
    assert 'ensure-directory --directory "$transaction_root"' in publisher
    receipt_directory_sync = publisher.index(
        '--directory "$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR"'
    )
    discovery = publisher.index(
        '--receipt-dir "$WINDOWS_ONLY_TRANSACTION_RECEIPT_DIR"'
    )
    prepared_record = publisher.index('python3 "$helper" "${prepared_args[@]}"')
    assert receipt_directory_sync < discovery < prepared_record
    assert 'flock -n "$lock_fd"' in publisher
    assert 'exec {lock_fd}>"$lock_dir/lease"' in publisher


def test_durable_activation_journal_rejects_replay_and_wrong_target(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, _receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    transaction_id = "windows-nightly-journal-0001"
    activation_receipt = tmp_path / "activation-live.json"
    transaction.activate(
        argparse.Namespace(
            target=incumbent,
            prepared=prepared,
            generation_receipt=tmp_path / "generation-live.json",
            transaction_id=transaction_id,
            receipt=activation_receipt,
        )
    )
    receipts = tmp_path / "receipts"
    receipts.mkdir()
    journal = receipts / "run.transaction.json"
    proof_dir = receipts / "run.activation-proofs"
    publication_receipt = receipts / "run.committed.json"
    current_receipt = tmp_path / "PUBLICATION_SCOPE.current.json"
    transaction.create_activation_journal(
        argparse.Namespace(
            transaction_id=transaction_id,
            activation_receipt=[activation_receipt],
            journal=journal,
            proof_dir=proof_dir,
            publication_receipt=publication_receipt,
            current_receipt=current_receipt,
        )
    )
    binding = transaction.activation_binding_from_journal(journal)
    activation_receipt.unlink()
    assert transaction.activation_binding_from_journal(journal) == binding
    assert binding["activationProofs"][0]["target"] == str(incumbent.resolve())
    assert binding["activationProofs"][0]["sha256"] == digest(
        proof_dir / "0000.activation.json"
    )

    with pytest.raises(transaction.TransactionError, match="replayed or mismatched"):
        transaction.create_activation_journal(
            argparse.Namespace(
                transaction_id="windows-nightly-replay-0002",
                activation_receipt=[proof_dir / "0000.activation.json"],
                journal=receipts / "replay.transaction.json",
                proof_dir=receipts / "replay.activation-proofs",
                publication_receipt=receipts / "replay.committed.json",
                current_receipt=tmp_path / "replay.current.json",
            )
        )

    wrong_target = tmp_path / "wrong-target"
    wrong_target.mkdir()
    (wrong_target / "old.bin").write_bytes(b"old shelf")
    forged = json.loads((proof_dir / "0000.activation.json").read_text(encoding="utf-8"))
    forged["target"] = str(wrong_target.resolve())
    forged_path = tmp_path / "wrong-target-activation.json"
    write_json(forged_path, forged)
    with pytest.raises(transaction.TransactionError, match="prepared inventory"):
        transaction.create_activation_journal(
            argparse.Namespace(
                transaction_id=transaction_id,
                activation_receipt=[forged_path],
                journal=receipts / "wrong-target.transaction.json",
                proof_dir=receipts / "wrong-target.activation-proofs",
                publication_receipt=receipts / "wrong-target.committed.json",
                current_receipt=tmp_path / "wrong-target.current.json",
            )
        )


def test_commit_marker_is_authoritative_on_both_sides_of_current_pointer_gap(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    transaction_id = "windows-nightly-commit-gap-0001"
    ephemeral = tmp_path / "activation-live.json"
    transaction.activate(
        argparse.Namespace(
            target=incumbent,
            prepared=prepared,
            generation_receipt=tmp_path / "generation-live.json",
            transaction_id=transaction_id,
            receipt=ephemeral,
        )
    )
    receipts = tmp_path / "receipts"
    receipts.mkdir()
    journal = receipts / "run.transaction.json"
    commit = receipts / "run.transaction.committed.json"
    publication_receipt = receipts / "run.committed.json"
    current = tmp_path / "PUBLICATION_SCOPE.current.json"
    transaction.create_activation_journal(
        argparse.Namespace(
            transaction_id=transaction_id,
            activation_receipt=[ephemeral],
            journal=journal,
            proof_dir=receipts / "run.activation-proofs",
            publication_receipt=publication_receipt,
            current_receipt=current,
        )
    )
    binding = transaction.activation_binding_from_journal(journal)
    write_json(
        publication_receipt,
        {
            "status": "passed",
            "transactionCommitRequired": True,
            "transactionCommitState": "awaiting_exact_commit_record",
            "windowsOnlyActivation": binding,
        },
    )

    # Before the commit marker, cleanup must discard the success-looking receipt
    # and restore the exact predecessor shelf.
    assert transaction.transaction_status(
        argparse.Namespace(journal=journal, commit=commit)
    )["status"] == "activated"
    transaction.discard_uncommitted_receipt(
        argparse.Namespace(journal=journal, commit=commit)
    )
    transaction.exchange(
        argparse.Namespace(
            left=incumbent,
            right=prepared,
            expected_left_inventory=receipt["preparedInventorySha256"],
            expected_right_inventory=receipt["incumbentInventorySha256"],
        )
    )
    assert transaction.transaction_status(
        argparse.Namespace(journal=journal, commit=commit)
    )["status"] == "rolled_back_pending_marker"
    rollback = receipts / "run.transaction.rolled-back.json"
    transaction.mark_transaction_rolled_back(
        argparse.Namespace(
            journal=journal,
            commit=commit,
            rollback=rollback,
        )
    )
    assert not publication_receipt.exists()
    assert not current.exists()
    assert rollback.is_file()
    assert transaction.canonical_sha256(transaction.inventory_tree(incumbent)) == receipt[
        "incumbentInventorySha256"
    ]

    # A separate activated transaction with a commit marker is never rolled back;
    # cleanup repairs the current pointer from the exact committed receipt.
    second = incumbent_shelf(tmp_path, values, "live-second")
    second_prepared, second_receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        second,
        "second",
    )
    second_ephemeral = tmp_path / "activation-second.json"
    second_id = "windows-nightly-commit-gap-0002"
    transaction.activate(
        argparse.Namespace(
            target=second,
            prepared=second_prepared,
            generation_receipt=tmp_path / "generation-second.json",
            transaction_id=second_id,
            receipt=second_ephemeral,
        )
    )
    second_journal = receipts / "second.transaction.json"
    second_commit = receipts / "second.transaction.committed.json"
    second_publication = receipts / "second.committed.json"
    second_current = tmp_path / "SECOND_PUBLICATION_SCOPE.current.json"
    transaction.create_activation_journal(
        argparse.Namespace(
            transaction_id=second_id,
            activation_receipt=[second_ephemeral],
            journal=second_journal,
            proof_dir=receipts / "second.activation-proofs",
            publication_receipt=second_publication,
            current_receipt=second_current,
        )
    )
    second_binding = transaction.activation_binding_from_journal(second_journal)
    write_json(
        second_publication,
        {
            "status": "passed",
            "transactionCommitRequired": True,
            "transactionCommitState": "awaiting_exact_commit_record",
            "windowsOnlyActivation": second_binding,
        },
    )
    transaction.commit_transaction(
        argparse.Namespace(journal=second_journal, commit=second_commit)
    )
    shutil.rmtree(second_prepared)
    assert transaction.transaction_status(
        argparse.Namespace(journal=second_journal, commit=second_commit)
    )["status"] == "committed"
    transaction.install_current_receipt(
        argparse.Namespace(journal=second_journal, commit=second_commit)
    )
    current_pointer = json.loads(second_current.read_text(encoding="utf-8"))
    assert current_pointer["status"] == "committed"
    assert current_pointer["transactionId"] == second_id
    assert current_pointer["publicationReceipt"]["path"] == str(second_publication)
    assert current_pointer["publicationReceipt"]["sha256"] == digest(second_publication)
    assert current_pointer["commitRecord"]["path"] == str(second_commit)
    assert current_pointer["commitRecord"]["sha256"] == digest(second_commit)
    assert transaction.canonical_sha256(transaction.inventory_tree(second)) == second_receipt[
        "preparedInventorySha256"
    ]


def test_pre_activation_failure_leaves_incumbent_unchanged(tmp_path: Path) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    before = transaction.inventory_tree(incumbent)
    prepared, receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    (prepared / "operator-note.txt").write_bytes(b"fault injection")

    with pytest.raises(transaction.TransactionError, match="right generation changed"):
        transaction.exchange(
            argparse.Namespace(
                left=incumbent,
                right=prepared,
                expected_left_inventory=receipt["incumbentInventorySha256"],
                expected_right_inventory=receipt["preparedInventorySha256"],
            )
        )
    assert transaction.inventory_tree(incumbent) == before


@pytest.mark.parametrize(
    "relative",
    ["files/chummer-avalonia-osx-arm64-installer.dmg", "operator-note.txt"],
)
def test_prepare_rejects_managed_or_ancillary_permission_mode_drift(
    tmp_path: Path, relative: str
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    path = incumbent / relative
    original_mode = stat.S_IMODE(path.stat().st_mode)
    path.chmod(0o600 if original_mode != 0o600 else 0o640)

    with pytest.raises(transaction.TransactionError, match="approved bytes or modes"):
        prepare_target(
            tmp_path,
            values["evidence_root"],
            values,
            proposal_path,
            final_path,
            incumbent,
            "live",
        )


def test_prepare_rejects_generation_nested_in_incumbent(tmp_path: Path) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    with pytest.raises(transaction.TransactionError, match="ancestor/descendant"):
        transaction.prepare_generation(
            argparse.Namespace(
                scope=final_path,
                proposal=proposal_path,
                evidence_root=values["evidence_root"],
                publication_dir=values["paths"]["publication_dir"],
                incumbent=incumbent,
                output_dir=incumbent / "nested-generation",
                receipt=tmp_path / "nested-generation.receipt.json",
            )
        )


def test_post_exchange_fsync_failure_rolls_back_exactly(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    incumbent_before = transaction.inventory_tree(incumbent)
    prepared_before = transaction.inventory_tree(prepared)
    original_fsync = transaction._fsync_exchange_parents
    calls = 0

    def fail_first_fsync(left: Path, right: Path) -> None:
        nonlocal calls
        calls += 1
        if calls == 1:
            raise OSError("injected post-rename fsync failure")
        original_fsync(left, right)

    monkeypatch.setattr(transaction, "_fsync_exchange_parents", fail_first_fsync)
    with pytest.raises(OSError, match="post-rename fsync failure"):
        transaction.exchange(
            argparse.Namespace(
                left=incumbent,
                right=prepared,
                expected_left_inventory=receipt["incumbentInventorySha256"],
                expected_right_inventory=receipt["preparedInventorySha256"],
            )
        )
    assert calls == 2
    assert transaction.inventory_tree(incumbent) == incumbent_before
    assert transaction.inventory_tree(prepared) == prepared_before


def test_activation_receipt_keyboard_interrupt_rolls_back_exactly(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, _ = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    incumbent_before = transaction.inventory_tree(incumbent)
    prepared_before = transaction.inventory_tree(prepared)

    def interrupt_receipt(_path: Path, _payload: dict[str, object]) -> None:
        raise KeyboardInterrupt("injected activation receipt interrupt")

    monkeypatch.setattr(transaction, "write_new_json", interrupt_receipt)
    with pytest.raises(KeyboardInterrupt, match="activation receipt interrupt"):
        transaction.activate(
            argparse.Namespace(
                target=incumbent,
                prepared=prepared,
                generation_receipt=tmp_path / "generation-live.json",
                transaction_id="windows-nightly-test-interrupt",
                receipt=tmp_path / "activation-live.json",
            )
        )
    assert transaction.inventory_tree(incumbent) == incumbent_before
    assert transaction.inventory_tree(prepared) == prepared_before


def test_activation_receipt_directory_fsync_failure_rolls_back_exactly(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, _ = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    incumbent_before = transaction.inventory_tree(incumbent)
    prepared_before = transaction.inventory_tree(prepared)
    activation_receipt = tmp_path / "activation-live.json"
    original_write = transaction.write_new_json
    original_directory_sync = transaction.fsync_directory
    writing_receipt = False
    failed = False

    def tracked_write(path: Path, payload: dict[str, object]) -> None:
        nonlocal writing_receipt
        writing_receipt = True
        try:
            original_write(path, payload)
        finally:
            writing_receipt = False

    def fail_receipt_directory_sync(path: Path) -> None:
        nonlocal failed
        if writing_receipt and Path(path) == activation_receipt.parent and not failed:
            failed = True
            raise OSError("injected activation receipt directory fsync failure")
        original_directory_sync(path)

    monkeypatch.setattr(transaction, "write_new_json", tracked_write)
    monkeypatch.setattr(transaction, "fsync_directory", fail_receipt_directory_sync)
    with pytest.raises(OSError, match="activation receipt directory fsync failure"):
        transaction.activate(
            argparse.Namespace(
                target=incumbent,
                prepared=prepared,
                generation_receipt=tmp_path / "generation-live.json",
                transaction_id="windows-nightly-test-directory-fsync",
                receipt=activation_receipt,
            )
        )

    assert failed is True
    assert not activation_receipt.exists()
    assert transaction.inventory_tree(incumbent) == incumbent_before
    assert transaction.inventory_tree(prepared) == prepared_before


def test_recover_activation_reverses_crash_gap_and_removes_ephemeral_receipt(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    prepared, receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    incumbent_before = transaction.inventory_tree(incumbent)
    activation_receipt = tmp_path / "activation-live.json"
    transaction.activate(
        argparse.Namespace(
            target=incumbent,
            prepared=prepared,
            generation_receipt=tmp_path / "generation-live.json",
            transaction_id="windows-nightly-blocked-0001",
            receipt=activation_receipt,
        )
    )
    activation_receipt = tmp_path / "activation-live.json"
    activation_receipt.write_bytes(b"simulated receipt before process termination")
    activation_receipt.chmod(0o600)

    recovered = transaction.recover_activation(
        argparse.Namespace(
            target=incumbent,
            prepared=prepared,
            incumbent_inventory=receipt["incumbentInventorySha256"],
            prepared_inventory=receipt["preparedInventorySha256"],
            activation_receipt=activation_receipt,
        )
    )
    assert recovered["status"] == "rolled_back"
    assert transaction.inventory_tree(incumbent) == incumbent_before
    assert not activation_receipt.exists()


def test_registry_ineligible_scope_writes_no_receipt_and_shelf_can_roll_back(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    incumbent = incumbent_shelf(tmp_path, values, "live")
    incumbent_before = transaction.inventory_tree(incumbent)
    prepared, receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        incumbent,
        "live",
    )
    activation_receipt = tmp_path / "activation-live.json"
    transaction.activate(
        argparse.Namespace(
            target=incumbent,
            prepared=prepared,
            generation_receipt=tmp_path / "generation-live.json",
            transaction_id="windows-nightly-blocked-0001",
            receipt=activation_receipt,
        )
    )
    committed = tmp_path / "receipts/run.committed.json"
    aborted = tmp_path / "receipts/run.aborted.json"
    current = tmp_path / "PUBLICATION_SCOPE.current.json"
    committed.parent.mkdir(parents=True)
    journal = committed.parent / "run.transaction.json"
    transaction.create_activation_journal(
        argparse.Namespace(
            transaction_id="windows-nightly-blocked-0001",
            activation_receipt=[activation_receipt],
            journal=journal,
            proof_dir=committed.parent / "run.activation-proofs",
            publication_receipt=committed,
            current_receipt=current,
        )
    )
    ineligible = json.loads(final_path.read_text(encoding="utf-8"))
    ineligible["registryFinalizeEligible"] = False
    write_json(final_path, ineligible)
    result = subprocess.run(
        [
            sys.executable,
            str(ROOT / "scripts/materialize-downloads-publication-scope.py"),
            "--output",
            str(committed),
            "--abort-output",
            str(aborted),
            "--deploy-dir",
            str(incumbent),
            "--release-version",
            str(values["version"]),
            "--release-channel",
            "preview",
            "--promoted-artifact-count",
            "2",
            "--require-external-publish",
            "--windows-publication-scope",
            str(final_path),
            "--windows-activation-journal",
            str(journal),
        ],
        text=True,
        capture_output=True,
        check=False,
    )
    assert result.returncode != 0
    assert not committed.exists()
    assert not current.exists()
    assert not aborted.exists()
    assert "invalid finalized Windows-only publication scope" in result.stderr
    transaction.exchange(
        argparse.Namespace(
            left=incumbent,
            right=prepared,
            expected_left_inventory=receipt["preparedInventorySha256"],
            expected_right_inventory=receipt["incumbentInventorySha256"],
        )
    )
    assert transaction.inventory_tree(incumbent) == incumbent_before


def test_successful_mirrors_equal_the_exact_approved_full_inventory(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    proposal = json.loads(proposal_path.read_text(encoding="utf-8"))
    first = incumbent_shelf(tmp_path, values, "live")
    second = incumbent_shelf(tmp_path, values, "mirror")
    first_prepared, first_receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        first,
        "live",
    )
    second_prepared, second_receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        second,
        "mirror",
    )
    for target, prepared, receipt in (
        (first, first_prepared, first_receipt),
        (second, second_prepared, second_receipt),
    ):
        transaction.exchange(
            argparse.Namespace(
                left=target,
                right=prepared,
                expected_left_inventory=receipt["incumbentInventorySha256"],
                expected_right_inventory=receipt["preparedInventorySha256"],
            )
        )
        assert scope.file_inventory(target) == proposal["fullShelfInventory"]
    assert scope.file_inventory(first) == scope.file_inventory(second)


def test_mirror_failure_can_roll_back_first_switch_exactly(tmp_path: Path) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    first = incumbent_shelf(tmp_path, values, "live")
    second = incumbent_shelf(tmp_path, values, "mirror")
    first_before = transaction.inventory_tree(first)
    second_before = transaction.inventory_tree(second)
    first_prepared, first_receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        first,
        "live",
    )
    second_prepared, second_receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        second,
        "mirror",
    )

    transaction.exchange(
        argparse.Namespace(
            left=first,
            right=first_prepared,
            expected_left_inventory=first_receipt["incumbentInventorySha256"],
            expected_right_inventory=first_receipt["preparedInventorySha256"],
        )
    )
    (second_prepared / "operator-note.txt").write_bytes(b"mirror fault")
    with pytest.raises(transaction.TransactionError):
        transaction.exchange(
            argparse.Namespace(
                left=second,
                right=second_prepared,
                expected_left_inventory=second_receipt["incumbentInventorySha256"],
                expected_right_inventory=second_receipt["preparedInventorySha256"],
            )
        )
    transaction.exchange(
        argparse.Namespace(
            left=first,
            right=first_prepared,
            expected_left_inventory=first_receipt["preparedInventorySha256"],
            expected_right_inventory=first_receipt["incumbentInventorySha256"],
        )
    )
    assert transaction.inventory_tree(first) == first_before
    assert transaction.inventory_tree(second) == second_before


def test_partial_multi_target_rollback_is_idempotently_resumable(
    tmp_path: Path,
) -> None:
    values, proposal_path, final_path = exact_evidence_fixture(tmp_path)
    first = incumbent_shelf(tmp_path, values, "live")
    second = incumbent_shelf(tmp_path, values, "mirror")
    first_prepared, first_receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        first,
        "live",
    )
    second_prepared, second_receipt = prepare_target(
        tmp_path,
        values["evidence_root"],
        values,
        proposal_path,
        final_path,
        second,
        "mirror",
    )
    transaction_id = "windows-nightly-partial-rollback-0001"
    activation_receipts = []
    for target, prepared, suffix in (
        (first, first_prepared, "live"),
        (second, second_prepared, "mirror"),
    ):
        activation_receipt = tmp_path / f"activation-{suffix}.json"
        transaction.activate(
            argparse.Namespace(
                target=target,
                prepared=prepared,
                generation_receipt=tmp_path / f"generation-{suffix}.json",
                transaction_id=transaction_id,
                receipt=activation_receipt,
            )
        )
        activation_receipts.append(activation_receipt)
    receipts = tmp_path / "receipts"
    receipts.mkdir()
    journal = receipts / "run.transaction.json"
    commit = receipts / "run.transaction.committed.json"
    rollback = receipts / "run.transaction.rolled-back.json"
    transaction.create_activation_journal(
        argparse.Namespace(
            transaction_id=transaction_id,
            activation_receipt=activation_receipts,
            journal=journal,
            proof_dir=receipts / "run.activation-proofs",
            publication_receipt=receipts / "run.committed.json",
            current_receipt=tmp_path / "PUBLICATION_SCOPE.current.json",
        )
    )

    # Simulate termination after the reverse-order rollback switched one mirror.
    transaction.exchange(
        argparse.Namespace(
            left=second,
            right=second_prepared,
            expected_left_inventory=second_receipt["preparedInventorySha256"],
            expected_right_inventory=second_receipt["incumbentInventorySha256"],
        )
    )
    assert transaction.transaction_status(
        argparse.Namespace(journal=journal, commit=commit)
    )["status"] == "partially_rolled_back"

    transaction.resume_transaction_rollback(
        argparse.Namespace(journal=journal, commit=commit)
    )
    assert transaction.transaction_status(
        argparse.Namespace(journal=journal, commit=commit)
    )["status"] == "rolled_back_pending_marker"
    # A repeated recovery after another restart is a no-op and remains valid.
    transaction.resume_transaction_rollback(
        argparse.Namespace(journal=journal, commit=commit)
    )
    transaction.mark_transaction_rolled_back(
        argparse.Namespace(journal=journal, commit=commit, rollback=rollback)
    )
    assert rollback.is_file()
    assert transaction.transaction_status(
        argparse.Namespace(journal=journal, commit=commit, rollback=rollback)
    )["status"] == "rolled_back"
    transaction.mark_transaction_rolled_back(
        argparse.Namespace(journal=journal, commit=commit, rollback=rollback)
    )
    assert transaction.canonical_sha256(transaction.inventory_tree(first)) == first_receipt[
        "incumbentInventorySha256"
    ]
    assert transaction.canonical_sha256(transaction.inventory_tree(second)) == second_receipt[
        "incumbentInventorySha256"
    ]
