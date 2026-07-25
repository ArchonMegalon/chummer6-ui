from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
from types import ModuleType

import pytest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "verify_keylocker_fixture_bundle.py"
FIXTURES = ROOT / "tests" / "fixtures" / "keylocker-signer-v1"
WORKFLOW = ROOT / ".github" / "workflows" / "pull-request-ci.yml"
FIXTURE_TEST = (
    ROOT / "tests" / "Chummer.KeyLockerSigner.FixtureTests" / "Program.cs"
)


def load_module() -> ModuleType:
    spec = importlib.util.spec_from_file_location(
        "keylocker_fixture_intake", SCRIPT
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


intake = load_module()


def sha256(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def write_contract(source_root: Path, fixture_root: Path) -> None:
    for relative in intake.SOURCE_FILES:
        path = source_root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(f"source:{relative}\n".encode())

    fixture_root.mkdir(parents=True)
    values = {
        "fixture-rfc3161-signature.der": b"\x30\x03\x02\x01\x00",
        "fixture-rfc3161-signed-installer.exe": b"MZpositive",
        "fixture-rfc3161-signed-installer.tampered.exe": b"MZtampered",
        "fixture-signed-without-timestamp.exe": b"MZno-timestamp",
        "local-fixture-code-signing.crt": (
            b"-----BEGIN CERTIFICATE-----\nY29kZQ==\n"
            b"-----END CERTIFICATE-----\n"
        ),
        "local-fixture-root.crt": (
            b"-----BEGIN CERTIFICATE-----\ncm9vdA==\n"
            b"-----END CERTIFICATE-----\n"
        ),
        "local-fixture-tsa.crt": (
            b"-----BEGIN CERTIFICATE-----\ndHNh\n"
            b"-----END CERTIFICATE-----\n"
        ),
    }
    for name, value in values.items():
        (fixture_root / name).write_bytes(value)

    source_rows = []
    for relative in intake.SOURCE_FILES:
        value = (source_root / relative).read_bytes()
        source_rows.append(
            {"path": relative, "sha256": sha256(value), "size": len(value)}
        )
    fixture_rows = []
    for name in sorted(intake.FIXTURE_ROLES):
        value = (fixture_root / name).read_bytes()
        fixture_rows.append(
            {
                "name": name,
                "role": intake.FIXTURE_ROLES[name],
                "sha256": sha256(value),
                "size": len(value),
            }
        )
    manifest = {
        "schema": intake.SCHEMA,
        "source": {
            "algorithm": "sha256",
            "digest": intake._inventory_digest(
                [
                    (row["path"], row["size"], row["sha256"])
                    for row in source_rows
                ]
            ),
            "files": source_rows,
        },
        "fixtureSet": {
            "algorithm": "sha256",
            "digest": intake._inventory_digest(
                [
                    (row["name"], row["size"], row["sha256"])
                    for row in fixture_rows
                ]
            ),
            "files": fixture_rows,
            "network": "forbidden",
            "privateKeyMaterial": "absent",
        },
    }
    (fixture_root / intake.MANIFEST_NAME).write_text(
        json.dumps(manifest, sort_keys=True, separators=(",", ":")) + "\n",
        encoding="utf-8",
    )


def synthetic_contract(tmp_path: Path) -> tuple[Path, Path]:
    source_root = tmp_path / "source"
    fixture_root = tmp_path / "fixtures"
    source_root.mkdir()
    write_contract(source_root, fixture_root)
    return source_root, fixture_root


def test_committed_fixture_bundle_matches_exact_current_signer_source() -> None:
    result = intake.verify(str(ROOT), str(FIXTURES))
    assert result == {
        "sourceDigest": (
            "ad598499239aa3cc08b764270f258be3a9dfc8c802b9d2c324f3fe5335e1f8a0"
        ),
        "fixtureSetDigest": (
            "be0a57b6c3b26c623b478dabcb3bc7bc90a2e9383bcace014d2d22fdc09d39b2"
        ),
    }


def test_intake_rejects_stale_source_pin(tmp_path: Path) -> None:
    source_root, fixture_root = synthetic_contract(tmp_path)
    stale = source_root / intake.SOURCE_FILES[1]
    stale.write_bytes(stale.read_bytes() + b"changed")

    with pytest.raises(
        intake.FixtureContractError, match="exact current source .* differs"
    ):
        intake.verify(str(source_root), str(fixture_root))


def test_intake_rejects_fixture_substitution(tmp_path: Path) -> None:
    source_root, fixture_root = synthetic_contract(tmp_path)
    fixture = fixture_root / "fixture-rfc3161-signed-installer.exe"
    fixture.write_bytes(fixture.read_bytes() + b"changed")

    with pytest.raises(
        intake.FixtureContractError, match="differs from its exact manifest pin"
    ):
        intake.verify(str(source_root), str(fixture_root))


def test_intake_rejects_symlinked_fixture(tmp_path: Path) -> None:
    source_root, fixture_root = synthetic_contract(tmp_path)
    name = "fixture-rfc3161-signed-installer.exe"
    fixture = fixture_root / name
    replacement = tmp_path / "replacement.exe"
    replacement.write_bytes(fixture.read_bytes())
    fixture.unlink()
    fixture.symlink_to(replacement)

    with pytest.raises(
        intake.FixtureContractError, match="single-link regular file"
    ):
        intake.verify(str(source_root), str(fixture_root))


def test_intake_rejects_unexpected_bundle_entry(tmp_path: Path) -> None:
    source_root, fixture_root = synthetic_contract(tmp_path)
    (fixture_root / "private-key.pfx").write_bytes(b"forbidden")

    with pytest.raises(
        intake.FixtureContractError, match="missing, unexpected, or nested"
    ):
        intake.verify(str(source_root), str(fixture_root))


def test_intake_rejects_noncanonical_manifest(tmp_path: Path) -> None:
    source_root, fixture_root = synthetic_contract(tmp_path)
    manifest_path = fixture_root / intake.MANIFEST_NAME
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest_path.write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )

    with pytest.raises(
        intake.FixtureContractError, match="not canonical JSON"
    ):
        intake.verify(str(source_root), str(fixture_root))


def test_pr_ci_mandates_intake_and_real_portable_verifier_fixtures() -> None:
    workflow = WORKFLOW.read_text(encoding="utf-8")
    fixture_test = FIXTURE_TEST.read_text(encoding="utf-8")
    assert workflow.count("tests/test_keylocker_fixture_intake.py") == 1
    assert workflow.count("verify_keylocker_fixture_bundle.py") == 1
    assert workflow.count("Chummer.KeyLockerSigner.FixtureTests.csproj") == 2
    assert workflow.count("--portable-ci") == 1
    assert "10.0.110" in workflow
    assert "secrets." not in workflow
    assert "fixture-rfc3161-signed-installer.exe" in fixture_test
    assert "AuthenticodeVerifier.Verify(" in fixture_test
    assert 'args[1] != "--portable-ci"' in fixture_test
