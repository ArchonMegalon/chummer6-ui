#!/usr/bin/env python3
"""Fail-closed intake for the offline KeyLocker verifier fixture bundle."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
from pathlib import Path
from typing import Any


SCHEMA = "chummer.keylocker_fixture_bundle.v1"
MANIFEST_NAME = "MANIFEST.json"
SHA256_RE = re.compile(r"[0-9a-f]{64}")
SOURCE_FILES = (
    ".github/workflows/pull-request-ci.yml",
    "scripts/Chummer.KeyLockerSigner/Chummer.KeyLockerSigner.csproj",
    "scripts/Chummer.KeyLockerSigner/Program.cs",
    "scripts/Chummer.KeyLockerSigner/global.json",
    "scripts/Chummer.KeyLockerSigner/packages.lock.json",
    "scripts/verify_keylocker_fixture_bundle.py",
    "tests/Chummer.KeyLockerSigner.FixtureTests/Chummer.KeyLockerSigner.FixtureTests.csproj",
    "tests/Chummer.KeyLockerSigner.FixtureTests/Program.cs",
    "tests/Chummer.KeyLockerSigner.FixtureTests/packages.lock.json",
)
FIXTURE_ROLES = {
    "fixture-rfc3161-signature.der": "authenticode_pkcs7_rfc3161",
    "fixture-rfc3161-signed-installer.exe": "positive_signed_pe",
    "fixture-rfc3161-signed-installer.tampered.exe": "tampered_signed_pe",
    "fixture-signed-without-timestamp.exe": "negative_no_timestamp_pe",
    "local-fixture-code-signing.crt": "public_code_signing_certificate",
    "local-fixture-root.crt": "public_test_trust_anchor",
    "local-fixture-tsa.crt": "public_timestamp_certificate",
}
MAX_MANIFEST_BYTES = 64 * 1024
MAX_SOURCE_BYTES = 4 * 1024 * 1024
MAX_FIXTURE_BYTES = 16 * 1024 * 1024


class FixtureContractError(RuntimeError):
    """The injected fixture bundle or its exact source binding is invalid."""


def _fail(message: str) -> None:
    raise FixtureContractError(message)


def _object_without_duplicates(
    pairs: list[tuple[str, Any]],
) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            _fail(f"manifest contains duplicate key {key!r}")
        result[key] = value
    return result


def _require_exact_keys(
    value: Any,
    keys: set[str],
    label: str,
) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != keys:
        _fail(f"{label} must contain exactly {sorted(keys)}")
    return value


def _normalized_absolute_directory(raw: str, label: str) -> Path:
    if not raw or "\x00" in raw or not os.path.isabs(raw):
        _fail(f"{label} must be a normalized absolute directory")
    path = Path(raw)
    if str(path) != os.path.normpath(raw):
        _fail(f"{label} must be a normalized absolute directory")
    try:
        resolved = path.resolve(strict=True)
    except OSError as error:
        _fail(f"{label} could not be resolved: {error}")
    if resolved != path or not path.is_dir():
        _fail(f"{label} must be a real directory without symlink traversal")
    return path


def _regular_file(path: Path, label: str, maximum_bytes: int) -> bytes:
    try:
        identity = path.lstat()
    except OSError as error:
        _fail(f"{label} is absent or unreadable: {error}")
    if (
        not stat.S_ISREG(identity.st_mode)
        or identity.st_nlink != 1
        or identity.st_size <= 0
        or identity.st_size > maximum_bytes
    ):
        _fail(
            f"{label} must be one nonempty single-link regular file no larger "
            f"than {maximum_bytes} bytes"
        )
    try:
        return path.read_bytes()
    except OSError as error:
        _fail(f"{label} could not be read: {error}")


def _sha256(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _exact_sha256(value: Any, label: str) -> str:
    if not isinstance(value, str) or SHA256_RE.fullmatch(value) is None:
        _fail(f"{label} must be one exact lowercase SHA-256")
    return value


def _exact_size(value: Any, label: str, maximum: int) -> int:
    if (
        not isinstance(value, int)
        or isinstance(value, bool)
        or value <= 0
        or value > maximum
    ):
        _fail(f"{label} must be a bounded positive byte size")
    return value


def _inventory_digest(rows: list[tuple[str, int, str]]) -> str:
    canonical = "".join(
        f"{digest} {size} {name}\n"
        for name, size, digest in sorted(rows)
    ).encode("utf-8")
    return _sha256(canonical)


def _validate_der_sequence(value: bytes, label: str) -> None:
    if len(value) < 2 or value[0] != 0x30:
        _fail(f"{label} must be one DER SEQUENCE")
    first = value[1]
    if first < 0x80:
        content_size = first
        header_size = 2
    else:
        length_size = first & 0x7F
        if length_size == 0 or length_size > 4 or len(value) < 2 + length_size:
            _fail(f"{label} has an invalid DER length")
        encoded_length = value[2 : 2 + length_size]
        if encoded_length[0] == 0:
            _fail(f"{label} has a non-minimal DER length")
        content_size = int.from_bytes(encoded_length, "big")
        if content_size < 0x80:
            _fail(f"{label} has a non-minimal DER length")
        header_size = 2 + length_size
    if header_size + content_size != len(value):
        _fail(f"{label} must contain exactly one DER value")


def _validate_fixture_shape(name: str, value: bytes) -> None:
    if name.endswith(".exe") and not value.startswith(b"MZ"):
        _fail(f"fixture {name} is not a PE image")
    if name.endswith(".crt") and not (
        value.startswith(b"-----BEGIN CERTIFICATE-----\n")
        and value.endswith(b"-----END CERTIFICATE-----\n")
    ):
        _fail(f"fixture {name} is not one canonical public PEM certificate")
    if name.endswith(".der"):
        _validate_der_sequence(value, f"fixture {name}")


def _load_manifest(path: Path) -> dict[str, Any]:
    raw = _regular_file(path, "KeyLocker fixture manifest", MAX_MANIFEST_BYTES)
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as error:
        _fail(f"KeyLocker fixture manifest is not UTF-8: {error}")
    try:
        value = json.loads(
            text,
            object_pairs_hook=_object_without_duplicates,
            parse_constant=lambda token: _fail(
                f"manifest contains forbidden JSON constant {token}"
            ),
        )
    except (json.JSONDecodeError, TypeError) as error:
        _fail(f"KeyLocker fixture manifest is invalid JSON: {error}")
    if not isinstance(value, dict):
        _fail("KeyLocker fixture manifest root must be one object")
    canonical = (
        json.dumps(
            value,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("utf-8")
    if raw != canonical:
        _fail("KeyLocker fixture manifest is not canonical JSON")
    return value


def verify(source_root_raw: str, fixture_root_raw: str) -> dict[str, str]:
    source_root = _normalized_absolute_directory(
        source_root_raw, "source root"
    )
    fixture_root = _normalized_absolute_directory(
        fixture_root_raw, "fixture root"
    )
    manifest = _require_exact_keys(
        _load_manifest(fixture_root / MANIFEST_NAME),
        {"schema", "source", "fixtureSet"},
        "manifest root",
    )
    if manifest["schema"] != SCHEMA:
        _fail(f"manifest schema must be {SCHEMA!r}")

    source = _require_exact_keys(
        manifest["source"],
        {"algorithm", "digest", "files"},
        "manifest source",
    )
    if source["algorithm"] != "sha256":
        _fail("manifest source algorithm must be sha256")
    source_digest = _exact_sha256(
        source["digest"], "manifest source digest"
    )
    if not isinstance(source["files"], list):
        _fail("manifest source files must be one array")
    if [row.get("path") for row in source["files"] if isinstance(row, dict)] != list(
        SOURCE_FILES
    ):
        _fail("manifest source files must name the exact ordered source set")

    source_rows: list[tuple[str, int, str]] = []
    for index, raw_row in enumerate(source["files"]):
        row = _require_exact_keys(
            raw_row,
            {"path", "sha256", "size"},
            f"manifest source files[{index}]",
        )
        relative = row["path"]
        if relative != SOURCE_FILES[index]:
            _fail("manifest source file order differs")
        expected_size = _exact_size(
            row["size"], f"source {relative} size", MAX_SOURCE_BYTES
        )
        expected_sha256 = _exact_sha256(
            row["sha256"], f"source {relative} SHA-256"
        )
        value = _regular_file(
            source_root / relative,
            f"exact current source {relative}",
            MAX_SOURCE_BYTES,
        )
        if len(value) != expected_size or _sha256(value) != expected_sha256:
            _fail(
                f"exact current source {relative} differs from the fixture "
                "manifest pin"
            )
        source_rows.append((relative, expected_size, expected_sha256))
    if _inventory_digest(source_rows) != source_digest:
        _fail("manifest source digest differs from its exact source inventory")

    fixture_set = _require_exact_keys(
        manifest["fixtureSet"],
        {"algorithm", "digest", "files", "network", "privateKeyMaterial"},
        "manifest fixtureSet",
    )
    if (
        fixture_set["algorithm"] != "sha256"
        or fixture_set["network"] != "forbidden"
        or fixture_set["privateKeyMaterial"] != "absent"
    ):
        _fail(
            "manifest fixtureSet must require sha256, forbidden network, and "
            "absent private key material"
        )
    fixture_digest = _exact_sha256(
        fixture_set["digest"], "manifest fixture-set digest"
    )
    if not isinstance(fixture_set["files"], list):
        _fail("manifest fixtureSet files must be one array")
    expected_names = sorted(FIXTURE_ROLES)
    actual_names = [
        row.get("name")
        for row in fixture_set["files"]
        if isinstance(row, dict)
    ]
    if actual_names != expected_names:
        _fail("manifest fixture files must name the exact ordered fixture set")

    try:
        directory_names = sorted(entry.name for entry in fixture_root.iterdir())
    except OSError as error:
        _fail(f"fixture root could not be enumerated: {error}")
    if directory_names != sorted([MANIFEST_NAME, *expected_names]):
        _fail("fixture root contains missing, unexpected, or nested entries")

    fixture_rows: list[tuple[str, int, str]] = []
    for index, raw_row in enumerate(fixture_set["files"]):
        row = _require_exact_keys(
            raw_row,
            {"name", "role", "sha256", "size"},
            f"manifest fixtureSet files[{index}]",
        )
        name = row["name"]
        if name != expected_names[index] or row["role"] != FIXTURE_ROLES[name]:
            _fail(f"fixture {name!r} has an unexpected identity or role")
        expected_size = _exact_size(
            row["size"], f"fixture {name} size", MAX_FIXTURE_BYTES
        )
        expected_sha256 = _exact_sha256(
            row["sha256"], f"fixture {name} SHA-256"
        )
        value = _regular_file(
            fixture_root / name, f"fixture {name}", MAX_FIXTURE_BYTES
        )
        if len(value) != expected_size or _sha256(value) != expected_sha256:
            _fail(f"fixture {name} differs from its exact manifest pin")
        _validate_fixture_shape(name, value)
        fixture_rows.append((name, expected_size, expected_sha256))
    if _inventory_digest(fixture_rows) != fixture_digest:
        _fail(
            "manifest fixture-set digest differs from its exact fixture "
            "inventory"
        )

    return {
        "sourceDigest": source_digest,
        "fixtureSetDigest": fixture_digest,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Validate an injected offline KeyLocker fixture bundle against "
            "the exact current signer source."
        )
    )
    parser.add_argument("--source-root", required=True)
    parser.add_argument("--fixture-root", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        result = verify(args.source_root, args.fixture_root)
    except FixtureContractError as error:
        print(f"[keylocker-fixture-intake] FAIL: {error}", file=os.sys.stderr)
        return 2
    print(
        "[keylocker-fixture-intake] PASS: "
        f"source={result['sourceDigest']} "
        f"fixtures={result['fixtureSetDigest']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
