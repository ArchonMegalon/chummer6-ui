from __future__ import annotations

import importlib.util
import base64
import subprocess
import stat
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT_PATH = ROOT / "scripts" / "materialize_local_portal_owner_secret.py"
SPEC = importlib.util.spec_from_file_location("portal_owner_secret_materializer", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def test_materializer_creates_private_strong_secret_and_reuses_it(tmp_path: Path) -> None:
    secret_directory = tmp_path / "portal-owner"

    secret_path = MODULE.materialize_portal_owner_secret(secret_directory)
    original_value = secret_path.read_text(encoding="utf-8")
    repeated_path = MODULE.materialize_portal_owner_secret(secret_directory)

    assert repeated_path == secret_path
    assert repeated_path.read_text(encoding="utf-8") == original_value
    assert len(original_value.encode("utf-8")) >= MODULE.MINIMUM_SECRET_BYTES
    assert original_value != MODULE.RETIRED_SAMPLE_SECRET
    assert stat.S_IMODE(secret_directory.stat().st_mode) == 0o700
    assert stat.S_IMODE(secret_path.stat().st_mode) == 0o600


def test_require_existing_never_materializes_missing_operator_secret(
    tmp_path: Path,
) -> None:
    secret_directory = tmp_path / "operator-secret"
    secret_directory.mkdir(mode=0o700)

    with pytest.raises(MODULE.PortalOwnerSecretError, match="required portal owner secret"):
        MODULE.materialize_portal_owner_secret(
            secret_directory,
            require_existing=True,
        )

    assert list(secret_directory.iterdir()) == []


def test_require_existing_rejects_short_or_broad_operator_secret(
    tmp_path: Path,
) -> None:
    secret_directory = tmp_path / "operator-secret"
    secret_directory.mkdir(mode=0o700)
    secret_path = secret_directory / MODULE.SECRET_FILE_NAME
    secret_path.write_text("short", encoding="utf-8")
    secret_path.chmod(0o600)

    with pytest.raises(MODULE.PortalOwnerSecretError, match="at least 32"):
        MODULE.materialize_portal_owner_secret(
            secret_directory,
            require_existing=True,
        )

    secret_path.write_text("x" * 48, encoding="utf-8")
    secret_path.chmod(0o644)
    with pytest.raises(MODULE.PortalOwnerSecretError, match="permissions are too broad"):
        MODULE.materialize_portal_owner_secret(
            secret_directory,
            require_existing=True,
        )


def test_materializer_rejects_symlink_secret(tmp_path: Path) -> None:
    secret_directory = tmp_path / "portal-owner"
    secret_directory.mkdir(mode=0o700)
    target = tmp_path / "outside-secret"
    target.write_text("x" * 48, encoding="utf-8")
    target.chmod(0o600)
    (secret_directory / MODULE.SECRET_FILE_NAME).symlink_to(target)

    with pytest.raises(MODULE.PortalOwnerSecretError, match="regular, non-symlink"):
        MODULE.materialize_portal_owner_secret(secret_directory)


def test_build_materializer_creates_exact_hmac_and_rsa_certificate(
    tmp_path: Path,
) -> None:
    secret_directory = tmp_path / "build"

    MODULE.materialize_build_secrets(secret_directory)

    encoded_hmac = (
        secret_directory / MODULE.BUILD_HMAC_FILE_NAME
    ).read_text(encoding="utf-8")
    assert len(base64.b64decode(encoded_hmac, validate=True)) == 32
    certificate_path = (
        secret_directory / "certificates" / "chummer-build-data-protection.p12"
    )
    password_path = secret_directory / MODULE.BUILD_CERTIFICATE_PASSWORD_FILE_NAME
    assert certificate_path.is_file()
    assert password_path.is_file()
    assert stat.S_IMODE(certificate_path.stat().st_mode) == 0o600


def test_hub_materializer_creates_rsa_3072_certificate(tmp_path: Path) -> None:
    secret_directory = tmp_path / "hub"

    certificate_path = MODULE.materialize_hub_secrets(secret_directory)
    password_path = secret_directory / MODULE.HUB_CERTIFICATE_PASSWORD_FILE_NAME
    extracted_path = tmp_path / "hub-certificate.pem"
    subprocess.run(
        [
            "openssl",
            "pkcs12",
            "-in",
            str(certificate_path),
            "-passin",
            f"file:{password_path}",
            "-clcerts",
            "-nokeys",
            "-out",
            str(extracted_path),
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    certificate_text = subprocess.run(
        ["openssl", "x509", "-in", str(extracted_path), "-noout", "-text"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout

    assert "Public-Key: (3072 bit)" in certificate_text
    assert "Key Encipherment" in certificate_text


def test_portal_harness_validates_explicit_secrets_as_runtime_uid() -> None:
    harness = (ROOT / "scripts" / "e2e-portal.sh").read_text(encoding="utf-8")

    assert "validate_runtime_secret_directory" in harness
    assert "--user 1654:1654" in harness
    assert 'target=/secrets,readonly"' in harness
    assert "--network none" in harness
    assert "test -d /secrets && test -x /secrets" in harness
