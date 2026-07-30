#!/usr/bin/env python3
"""Materialize or validate the local portal owner-propagation secret."""

from __future__ import annotations

import argparse
import base64
import os
import secrets
import stat
import subprocess
import sys
import tempfile
import uuid
from pathlib import Path


SECRET_FILE_NAME = "CHUMMER_PORTAL_OWNER_SHARED_KEY"
MINIMUM_SECRET_BYTES = 32
RETIRED_SAMPLE_SECRET = "local-self-hosted-portal-shared-key"
BUILD_HMAC_FILE_NAME = "CHUMMER_BUILD_OWNER_CHANNEL_HMAC_KEY_BASE64"
BUILD_CERTIFICATE_PASSWORD_FILE_NAME = (
    "CHUMMER_BLAZOR_DATA_PROTECTION_CERTIFICATE_PASSWORD"
)
HUB_CERTIFICATE_PASSWORD_FILE_NAME = (
    "CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PASSWORD"
)


class PortalOwnerSecretError(RuntimeError):
    """Raised when the local secret directory is unsafe or incomplete."""


def _validate_secret_value(value: str) -> None:
    if value != value.strip() or any(ord(character) < 32 for character in value):
        raise PortalOwnerSecretError(
            f"{SECRET_FILE_NAME} must not contain leading, trailing, or control characters"
        )
    if len(value.encode("utf-8")) < MINIMUM_SECRET_BYTES:
        raise PortalOwnerSecretError(
            f"{SECRET_FILE_NAME} must contain at least {MINIMUM_SECRET_BYTES} UTF-8 bytes"
        )
    if value == RETIRED_SAMPLE_SECRET:
        raise PortalOwnerSecretError(
            f"{SECRET_FILE_NAME} must not use the retired self-host sample value"
        )


def _validate_private_mode(path: Path, expected_kind: str) -> None:
    mode = stat.S_IMODE(path.stat().st_mode)
    if mode & 0o077:
        raise PortalOwnerSecretError(
            f"{expected_kind} permissions are too broad at {path}; expected no group/world access"
        )


def _read_existing_secret(
    secret_path: Path,
    validator=_validate_secret_value,
) -> str:
    if secret_path.is_symlink() or not secret_path.is_file():
        raise PortalOwnerSecretError(
            f"{SECRET_FILE_NAME} must be a regular, non-symlink file at {secret_path}"
        )
    _validate_private_mode(secret_path, SECRET_FILE_NAME)
    value = secret_path.read_text(encoding="utf-8")
    validator(value)
    return value


def _write_secret_atomically(secret_path: Path, value: str | bytes) -> None:
    temporary_path = secret_path.with_name(
        f".{secret_path.name}.{uuid.uuid4().hex}.tmp"
    )
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW

    descriptor = os.open(temporary_path, flags, 0o600)
    try:
        encoded = value.encode("utf-8") if isinstance(value, str) else value
        with os.fdopen(descriptor, "wb", closefd=False) as stream:
            stream.write(encoded)
            stream.flush()
            os.fsync(stream.fileno())
        os.close(descriptor)
        descriptor = -1
        os.replace(temporary_path, secret_path)
        os.chmod(secret_path, 0o600, follow_symlinks=False)
        directory_descriptor = os.open(secret_path.parent, os.O_RDONLY)
        try:
            os.fsync(directory_descriptor)
        finally:
            os.close(directory_descriptor)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        temporary_path.unlink(missing_ok=True)


def _prepare_private_directory(directory: Path) -> Path:
    directory = directory.expanduser()
    if directory.is_symlink():
        raise PortalOwnerSecretError(
            f"secret directory must not be a symlink: {directory}"
        )
    directory.mkdir(mode=0o700, parents=True, exist_ok=True)
    if directory.is_symlink() or not directory.is_dir():
        raise PortalOwnerSecretError(
            f"secret directory must be a regular directory: {directory}"
        )
    os.chmod(directory, 0o700, follow_symlinks=False)
    return directory


def materialize_portal_owner_secret(
    directory: Path,
    *,
    require_existing: bool = False,
) -> Path:
    directory = directory.expanduser()
    if directory.is_symlink():
        raise PortalOwnerSecretError(
            f"portal owner secret directory must not be a symlink: {directory}"
        )

    if require_existing:
        if not directory.is_dir():
            raise PortalOwnerSecretError(
                f"portal owner secret directory does not exist: {directory}"
            )
        _validate_private_mode(directory, "portal owner secret directory")
    else:
        _prepare_private_directory(directory)

    secret_path = directory / SECRET_FILE_NAME
    if secret_path.exists() or secret_path.is_symlink():
        _read_existing_secret(secret_path)
        return secret_path

    if require_existing:
        raise PortalOwnerSecretError(
            f"required portal owner secret is missing: {secret_path}"
        )

    value = secrets.token_urlsafe(48)
    _validate_secret_value(value)
    _write_secret_atomically(secret_path, value)
    _read_existing_secret(secret_path)
    return secret_path


def _materialize_text_secret(
    directory: Path,
    file_name: str,
    value_factory,
    validator,
) -> Path:
    secret_path = directory / file_name
    if secret_path.exists() or secret_path.is_symlink():
        _read_existing_secret(secret_path, validator)
        return secret_path

    value = value_factory()
    validator(value)
    _write_secret_atomically(secret_path, value)
    _read_existing_secret(secret_path, validator)
    return secret_path


def _validate_hmac_key(value: str) -> None:
    if value != value.strip():
        raise PortalOwnerSecretError(
            f"{BUILD_HMAC_FILE_NAME} must not contain surrounding whitespace"
        )
    try:
        decoded = base64.b64decode(value, validate=True)
    except ValueError as exc:
        raise PortalOwnerSecretError(
            f"{BUILD_HMAC_FILE_NAME} must contain valid Base64"
        ) from exc
    if len(decoded) != 32:
        raise PortalOwnerSecretError(
            f"{BUILD_HMAC_FILE_NAME} must encode exactly 32 bytes"
        )


def _validate_password(value: str) -> None:
    _validate_secret_value(value)


def _run_openssl(arguments: list[str], purpose: str) -> subprocess.CompletedProcess[str]:
    completed = subprocess.run(
        ["openssl", *arguments],
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if completed.returncode != 0:
        raise PortalOwnerSecretError(f"OpenSSL could not {purpose}")
    return completed


def _validate_certificate(
    certificate_path: Path,
    password_path: Path,
    minimum_rsa_bits: int,
) -> None:
    if certificate_path.is_symlink() or not certificate_path.is_file():
        raise PortalOwnerSecretError(
            f"certificate must be a regular, non-symlink file at {certificate_path}"
        )
    _validate_private_mode(certificate_path, "certificate")
    with tempfile.TemporaryDirectory(prefix="chummer-local-certificate-check-") as root:
        public_certificate_path = Path(root) / "certificate.pem"
        _run_openssl(
            [
                "pkcs12",
                "-in",
                str(certificate_path),
                "-passin",
                f"file:{password_path}",
                "-clcerts",
                "-nokeys",
                "-out",
                str(public_certificate_path),
            ],
            "open the local PKCS#12 certificate",
        )
        _run_openssl(
            ["x509", "-in", str(public_certificate_path), "-noout", "-checkend", "0"],
            "validate the local certificate lifetime",
        )
        certificate_text = _run_openssl(
            ["x509", "-in", str(public_certificate_path), "-noout", "-text"],
            "inspect the local certificate",
        ).stdout

    if f"Public-Key: ({minimum_rsa_bits} bit)" not in certificate_text:
        raise PortalOwnerSecretError(
            f"local certificate must use at least RSA-{minimum_rsa_bits}"
        )
    if "Key Encipherment" not in certificate_text:
        raise PortalOwnerSecretError(
            "local certificate key usage must include key encipherment"
        )


def _materialize_certificate(
    directory: Path,
    *,
    certificate_name: str,
    password_path: Path,
    rsa_bits: int,
    common_name: str,
) -> Path:
    certificates_directory = directory / "certificates"
    certificates_directory.mkdir(mode=0o700, exist_ok=True)
    if certificates_directory.is_symlink() or not certificates_directory.is_dir():
        raise PortalOwnerSecretError(
            f"certificate directory must be a regular directory: {certificates_directory}"
        )
    os.chmod(certificates_directory, 0o700, follow_symlinks=False)
    certificate_path = certificates_directory / certificate_name
    if certificate_path.exists() or certificate_path.is_symlink():
        _validate_certificate(certificate_path, password_path, rsa_bits)
        return certificate_path

    with tempfile.TemporaryDirectory(prefix="chummer-local-certificate-build-") as root:
        temporary_root = Path(root)
        key_path = temporary_root / "key.pem"
        public_certificate_path = temporary_root / "certificate.pem"
        pkcs12_path = temporary_root / "certificate.p12"
        _run_openssl(
            [
                "req",
                "-x509",
                "-newkey",
                f"rsa:{rsa_bits}",
                "-sha256",
                "-days",
                "3650",
                "-nodes",
                "-subj",
                f"/CN={common_name}",
                "-addext",
                "keyUsage=critical,digitalSignature,keyEncipherment",
                "-keyout",
                str(key_path),
                "-out",
                str(public_certificate_path),
            ],
            "generate the local RSA certificate",
        )
        _run_openssl(
            [
                "pkcs12",
                "-export",
                "-in",
                str(public_certificate_path),
                "-inkey",
                str(key_path),
                "-passout",
                f"file:{password_path}",
                "-out",
                str(pkcs12_path),
            ],
            "package the local RSA certificate",
        )
        _write_secret_atomically(certificate_path, pkcs12_path.read_bytes())

    _validate_certificate(certificate_path, password_path, rsa_bits)
    return certificate_path


def materialize_build_secrets(directory: Path) -> Path:
    directory = _prepare_private_directory(directory)
    hmac_path = _materialize_text_secret(
        directory,
        BUILD_HMAC_FILE_NAME,
        lambda: base64.b64encode(secrets.token_bytes(32)).decode("ascii"),
        _validate_hmac_key,
    )
    password_path = _materialize_text_secret(
        directory,
        BUILD_CERTIFICATE_PASSWORD_FILE_NAME,
        lambda: secrets.token_urlsafe(48),
        _validate_password,
    )
    _materialize_certificate(
        directory,
        certificate_name="chummer-build-data-protection.p12",
        password_path=password_path,
        rsa_bits=2048,
        common_name="Chummer Local Build Data Protection",
    )
    return hmac_path


def materialize_hub_secrets(directory: Path) -> Path:
    directory = _prepare_private_directory(directory)
    password_path = _materialize_text_secret(
        directory,
        HUB_CERTIFICATE_PASSWORD_FILE_NAME,
        lambda: secrets.token_urlsafe(48),
        _validate_password,
    )
    return _materialize_certificate(
        directory,
        certificate_name="chummer-hub-data-protection.p12",
        password_path=password_path,
        rsa_bits=3072,
        common_name="Chummer Local Hub Data Protection",
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument(
        "--kind",
        choices=("portal-owner", "build", "hub"),
        default="portal-owner",
    )
    parser.add_argument(
        "--require-existing",
        action="store_true",
        help="Validate an operator-supplied directory without creating or changing it.",
    )
    args = parser.parse_args(argv)

    try:
        if args.require_existing:
            if args.kind != "portal-owner":
                raise PortalOwnerSecretError(
                    "--require-existing is supported only for portal-owner validation"
                )
            secret_path = materialize_portal_owner_secret(
                args.directory,
                require_existing=True,
            )
        elif args.kind == "build":
            secret_path = materialize_build_secrets(args.directory)
        elif args.kind == "hub":
            secret_path = materialize_hub_secrets(args.directory)
        else:
            secret_path = materialize_portal_owner_secret(args.directory)
    except (OSError, UnicodeError, PortalOwnerSecretError) as exc:
        print(f"portal_owner_secret:error:{exc}", file=sys.stderr)
        return 2

    print(f"portal_owner_secret:ready:{secret_path.parent}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
