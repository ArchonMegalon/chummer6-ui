from __future__ import annotations

import copy
import base64
import hashlib
import importlib.util
import json
import lzma
import os
import shutil
import socket
import subprocess
import sys
from collections.abc import Callable
from datetime import UTC, datetime, timedelta
from pathlib import Path
from types import SimpleNamespace

import pytest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "linux_deb_signing.py"
SPEC = importlib.util.spec_from_file_location("linux_deb_signing", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

FINGERPRINT = "0123456789ABCDEFFEDCBA987654321001234567"
LONG_KEY_ID = FINGERPRINT[-16:]
RELEASE_VERSION = "run-20260726-120000"
SOURCE_SHA = "1" * 40
EXTRACTED_TOOL_ROOT = os.environ.get("CHUMMER_DEBSIG_TEST_TOOL_ROOT", "")


def real_tool_path(name: str) -> Path:
    if EXTRACTED_TOOL_ROOT and name in {"debsigs", "debsig-verify"}:
        return Path(EXTRACTED_TOOL_ROOT) / "usr" / "bin" / name
    return Path("/usr/bin") / name


REAL_TOOLS_AVAILABLE = all(
    real_tool_path(name).is_file()
    for name in ("debsigs", "debsig-verify", "gpg", "gpgv")
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def ar_member(name: str, data: bytes) -> bytes:
    encoded_name = f"{name}/"
    assert len(encoded_name) <= 16
    header = (
        f"{encoded_name:<16}"
        f"{0:<12}"
        f"{0:<6}"
        f"{0:<6}"
        f"{0o100644:<8}"
        f"{len(data):<10}"
        "`\n"
    ).encode("ascii")
    assert len(header) == 60
    return header + data + (b"\n" if len(data) % 2 else b"")


def deb_bytes(*, signed: bool) -> bytes:
    members = [
        ar_member("debian-binary", b"2.0\n"),
        ar_member(
            "control.tar.xz",
            lzma.compress(b"control-fixture", format=lzma.FORMAT_XZ),
        ),
        ar_member(
            "data.tar.xz",
            lzma.compress(
                b"authenticated-data-member-fixture",
                format=lzma.FORMAT_XZ,
            ),
        ),
    ]
    if signed:
        members.append(ar_member("_gpgorigin", b"signature-fixture"))
    return b"!<arch>\n" + b"".join(members)


def fixture_files(tmp_path: Path) -> tuple[
    MODULE.Snapshot, MODULE.Snapshot, MODULE.Snapshot
]:
    package_path = tmp_path / MODULE.ARTIFACT_FILE_NAME
    package_path.write_bytes(deb_bytes(signed=True))
    policy_path = (
        tmp_path
        / "signing"
        / "policies"
        / LONG_KEY_ID
        / MODULE.POLICY_FILE_NAME
    )
    keyring_path = (
        tmp_path
        / "signing"
        / "keyrings"
        / LONG_KEY_ID
        / MODULE.KEYRING_FILE_NAME
    )
    policy_path.parent.mkdir(parents=True)
    keyring_path.parent.mkdir(parents=True)
    policy_path.write_bytes(
        MODULE.policy_bytes(FINGERPRINT, MODULE.KEYRING_FILE_NAME)
    )
    keyring_path.write_bytes(b"public-keyring-fixture")
    return (
        MODULE.snapshot(package_path, "package", MODULE.MAX_PACKAGE_BYTES),
        MODULE.snapshot(policy_path, "policy", MODULE.MAX_JSON_BYTES),
        MODULE.snapshot(keyring_path, "keyring", MODULE.MAX_KEY_BYTES),
    )


def transaction_fixture(
    root: Path,
) -> tuple[SimpleNamespace, dict[str, MODULE.Snapshot], MODULE.Snapshot]:
    members = MODULE._canonical_transaction_members(FINGERPRINT)
    paths = {
        "package": root.joinpath(
            *Path(members["package"]).parts
        ),
        "policy": root.joinpath(*Path(members["policy"]).parts),
        "publicKeyring": root.joinpath(
            *Path(members["publicKeyring"]).parts
        ),
        "signingReceipt": root.joinpath(
            *Path(members["signingReceipt"]).parts
        ),
        "signedExportReceipt": root.joinpath(
            *Path(members["signedExportReceipt"]).parts
        ),
    }
    content = {
        "package": deb_bytes(signed=True),
        "policy": b"governed policy",
        "publicKeyring": b"governed keyring",
        "signingReceipt": b"governed signing receipt",
        "signedExportReceipt": b"governed signed export receipt",
    }
    limits = {
        "package": MODULE.MAX_PACKAGE_BYTES,
        "policy": MODULE.MAX_JSON_BYTES,
        "publicKeyring": MODULE.MAX_KEY_BYTES,
        "signingReceipt": MODULE.MAX_JSON_BYTES,
        "signedExportReceipt": MODULE.MAX_JSON_BYTES,
    }
    snapshots: dict[str, MODULE.Snapshot] = {}
    for key, path in paths.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content[key])
        snapshots[key] = MODULE.snapshot(
            path, f"fixture {key}", limits[key]
        )
    payload = MODULE._transaction_payload(
        outputs=snapshots, members=members
    )
    manifest_path = root / MODULE.TRANSACTION_MANIFEST_FILE_NAME
    manifest_path.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    manifest = MODULE.snapshot(
        manifest_path, "fixture manifest", MODULE.MAX_JSON_BYTES
    )
    args = SimpleNamespace(
        package=paths["package"],
        policy=paths["policy"],
        public_keyring=paths["publicKeyring"],
        receipt=paths["signingReceipt"],
        signed_export_receipt=paths["signedExportReceipt"],
        transaction_manifest=manifest_path,
        expected_primary_fingerprint=FINGERPRINT,
        expected_transaction_manifest_sha256=manifest.sha256,
    )
    return args, snapshots, manifest


def tool_row(name: str, version: str) -> dict[str, str]:
    return {
        "binarySha256": "a" * 64,
        "packageName": name,
        "packageVersion": version,
    }


def receipt(
    package: MODULE.Snapshot,
    policy: MODULE.Snapshot,
    keyring: MODULE.Snapshot,
) -> dict[str, object]:
    created = int(datetime.now(UTC).timestamp())
    created_at = (
        datetime.fromtimestamp(created, UTC)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )
    signer = {
        "longKeyId": LONG_KEY_ID,
        "primaryFingerprint": FINGERPRINT,
        "signingFingerprint": FINGERPRINT,
    }
    return {
        "app": "avalonia",
        "artifactSignatures": [
            {
                "artifactFileName": MODULE.ARTIFACT_FILE_NAME,
                "artifactSha256": package.sha256,
                "artifactSizeBytes": package.size_bytes,
                "cryptographicVerification": "passed",
                "digestAlgorithm": "sha256",
                "signatureType": "origin",
                "signer": signer,
                "verifier": {
                    "backend": "debsig-verify",
                    "openPgpSignature": {
                        "createdAt": created_at,
                        "creationTimestamp": created,
                        "fingerprint": FINGERPRINT,
                        "hashAlgorithm": "sha256",
                        "primaryFingerprint": FINGERPRINT,
                        "publicKeyAlgorithm": "rsa",
                    },
                    "policySha256": policy.sha256,
                    "positiveExitCode": 0,
                    "providerIndependent": True,
                    "publicKeyringSha256": keyring.sha256,
                    "tamperNegative": {
                        "expectedExitCode": 13,
                        "mutation": "data-member-byte-flip",
                        "observedExitCode": 13,
                        "status": "rejected",
                    },
                },
            }
        ],
        "artifacts": [
            {
                "fileName": MODULE.ARTIFACT_FILE_NAME,
                "kind": "installer",
                "sha256": package.sha256,
                "signingStatus": "pass",
            }
        ],
        "contractName": MODULE.SIGNING_CONTRACT,
        "contractVersion": 2,
        "digestAlgorithm": "sha256",
        "generatedAt": created_at,
        "platform": "linux",
        "releaseChannel": "stable",
        "releaseVersion": RELEASE_VERSION,
        "rid": "linux-x64",
        "signer": signer,
        "signingBackend": "debsigs-origin-openpgp",
        "signingStatus": "pass",
        "source": {
            "actor": "release-operator",
            "environment": "linux-deb-signing",
            "ref": "refs/heads/main",
            "repository": "ArchonMegalon/chummer6-ui",
            "runAttempt": "1",
            "runId": "123",
            "sha": SOURCE_SHA,
            "workflow": ".github/workflows/linux-native-candidate-export.yml",
        },
        "tools": {
            "debsigVerify": tool_row("debsig-verify", "0.29"),
            "debsigs": tool_row("debsigs", "0.1.26"),
            "gpg": tool_row("gpg", "2.4.4-fixture"),
            "gpgv": tool_row("gpgv", "2.4.4-fixture"),
        },
        "verificationMaterial": {
            "policy": {
                "memberPath": (
                    f"signing/policies/{LONG_KEY_ID}/"
                    f"{MODULE.POLICY_FILE_NAME}"
                ),
                "sha256": policy.sha256,
                "sizeBytes": policy.size_bytes,
            },
            "publicKeyring": {
                "memberPath": (
                    f"signing/keyrings/{LONG_KEY_ID}/"
                    f"{MODULE.KEYRING_FILE_NAME}"
                ),
                "sha256": keyring.sha256,
                "sizeBytes": keyring.size_bytes,
            },
        },
    }


def test_policy_is_full_fingerprint_pinned_and_canonical() -> None:
    policy = MODULE.policy_bytes(FINGERPRINT, MODULE.KEYRING_FILE_NAME)

    assert policy.count(FINGERPRINT.encode("ascii")) == 3
    assert LONG_KEY_ID.encode("ascii") in FINGERPRINT.encode("ascii")
    assert b'<Required Type="origin"' in policy
    assert policy.endswith(b"</Policy>\n")


def test_valid_v2_receipt_binds_exact_signed_bytes_and_material(
    tmp_path: Path,
) -> None:
    package, policy, keyring = fixture_files(tmp_path)

    projection = MODULE.validate_signing_receipt(
        receipt(package, policy, keyring),
        package=package,
        policy=policy,
        keyring=keyring,
        release_version=RELEASE_VERSION,
    )

    assert projection["signer"]["primaryFingerprint"] == FINGERPRINT
    assert projection["openPgpSignature"]["fingerprint"] == FINGERPRINT
    assert (
        projection["verificationMaterial"]["publicKeyring"]["sha256"]
        == keyring.sha256
    )


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (
            lambda payload: payload["signer"].update(
                {
                    "longKeyId": "A" * 16,
                    "signingFingerprint": "A" * 40,
                }
            ),
            "pinned primary key",
        ),
        (
            lambda payload: payload["artifactSignatures"][0][
                "verifier"
            ].__setitem__("publicKeyringSha256", "b" * 64),
            "verifier evidence",
        ),
        (
            lambda payload: payload["artifactSignatures"][0][
                "verifier"
            ]["tamperNegative"].__setitem__("observedExitCode", 0),
            "verifier evidence",
        ),
        (
            lambda payload: payload["verificationMaterial"]["policy"].__setitem__(
                "memberPath", f"signing/policies/{'A' * 16}/bad.pol"
            ),
            "verification material",
        ),
    ],
)
def test_v2_receipt_fails_closed_on_security_pin_drift(
    tmp_path: Path,
    mutate: object,
    message: str,
) -> None:
    package, policy, keyring = fixture_files(tmp_path)
    payload = copy.deepcopy(receipt(package, policy, keyring))
    mutate(payload)

    with pytest.raises(MODULE.ContractError, match=message):
        MODULE.validate_signing_receipt(
            payload,
            package=package,
            policy=policy,
            keyring=keyring,
            release_version=RELEASE_VERSION,
        )


def test_v2_receipt_rejects_stale_or_post_receipt_signature_time(
    tmp_path: Path,
) -> None:
    package, policy, keyring = fixture_files(tmp_path)
    stale = copy.deepcopy(receipt(package, policy, keyring))
    stale_time = datetime.now(UTC).replace(microsecond=0) - timedelta(
        seconds=MODULE.MAX_RECEIPT_AGE_SECONDS + 1
    )
    stale["generatedAt"] = stale_time.isoformat().replace("+00:00", "Z")
    with pytest.raises(MODULE.ContractError, match="future-dated or stale"):
        MODULE.validate_signing_receipt(
            stale,
            package=package,
            policy=policy,
            keyring=keyring,
            release_version=RELEASE_VERSION,
        )

    post_receipt = copy.deepcopy(receipt(package, policy, keyring))
    generated = datetime.strptime(
        post_receipt["generatedAt"], "%Y-%m-%dT%H:%M:%SZ"
    ).replace(tzinfo=UTC)
    signature = generated + timedelta(seconds=1)
    openpgp = post_receipt["artifactSignatures"][0]["verifier"][
        "openPgpSignature"
    ]
    openpgp["creationTimestamp"] = int(signature.timestamp())
    openpgp["createdAt"] = signature.isoformat().replace("+00:00", "Z")
    with pytest.raises(MODULE.ContractError, match="freshness window"):
        MODULE.validate_signing_receipt(
            post_receipt,
            package=package,
            policy=policy,
            keyring=keyring,
            release_version=RELEASE_VERSION,
        )


def test_signed_export_v3_rejects_source_or_material_replay(
    tmp_path: Path,
) -> None:
    package, policy, keyring = fixture_files(tmp_path)
    signing_payload = receipt(package, policy, keyring)
    signing_projection = MODULE.validate_signing_receipt(
        signing_payload,
        package=package,
        policy=policy,
        keyring=keyring,
        release_version=RELEASE_VERSION,
    )
    signing_path = tmp_path / MODULE.SIGNING_RECEIPT_FILE_NAME
    signing_path.write_text(
        json.dumps(signing_payload) + "\n", encoding="utf-8"
    )
    signing = MODULE.snapshot(
        signing_path, "signing receipt", MODULE.MAX_JSON_BYTES
    )
    source = dict(signing_projection["source"])
    source.pop("environment")
    payload = {
        "artifact": {
            "fileName": MODULE.ARTIFACT_FILE_NAME,
            "memberPath": f"files/{MODULE.ARTIFACT_FILE_NAME}",
            "sha256": package.sha256,
            "sizeBytes": package.size_bytes,
        },
        "contractName": MODULE.EXPORT_CONTRACT,
        "contractVersion": 3,
        "generatedAt": signing_projection["generatedAt"],
        "livePredecessorAuthority": {
            "liveReleaseChannelSha256": "1" * 64,
            "nMinusOneReleaseSha256": "2" * 64,
            "selectedTupleSha256": "3" * 64,
        },
        "nonPublishing": True,
        "package": {
            "architecture": "amd64",
            "name": "chummer6-avalonia",
            "version": MODULE.normalize_debian_version(RELEASE_VERSION),
        },
        "publicKeyring": signing_payload["verificationMaterial"][
            "publicKeyring"
        ],
        "releaseVersion": RELEASE_VERSION,
        "signingReceipt": {
            "memberPath": f"signing/{MODULE.SIGNING_RECEIPT_FILE_NAME}",
            "sha256": signing.sha256,
            "sizeBytes": signing.size_bytes,
        },
        "source": source,
        "status": "signed",
        "unsignedArtifact": {
            "fileName": MODULE.ARTIFACT_FILE_NAME,
            "memberPath": f"files/{MODULE.ARTIFACT_FILE_NAME}",
            "sha256": "8" * 64,
            "sizeBytes": package.size_bytes - 1,
        },
        "verificationPolicy": signing_payload["verificationMaterial"][
            "policy"
        ],
    }
    MODULE.validate_signed_export_receipt(
        payload,
        signed=package,
        signing_receipt=signing,
        policy=policy,
        keyring=keyring,
        signing_projection=signing_projection,
        release_version=RELEASE_VERSION,
    )

    replayed = copy.deepcopy(payload)
    replayed["source"]["sha"] = "2" * 40
    with pytest.raises(MODULE.ContractError, match="source differs"):
        MODULE.validate_signed_export_receipt(
            replayed,
            signed=package,
            signing_receipt=signing,
            policy=policy,
            keyring=keyring,
            signing_projection=signing_projection,
            release_version=RELEASE_VERSION,
        )

    wrong_material = copy.deepcopy(payload)
    wrong_material["signingReceipt"]["sha256"] = "f" * 64
    with pytest.raises(MODULE.ContractError, match="binding differs"):
        MODULE.validate_signed_export_receipt(
            wrong_material,
            signed=package,
            signing_receipt=signing,
            policy=policy,
            keyring=keyring,
            signing_projection=signing_projection,
            release_version=RELEASE_VERSION,
        )


def test_unsigned_archive_shape_fails_before_secret_decode(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    unsigned = tmp_path / MODULE.ARTIFACT_FILE_NAME
    unsigned.write_bytes(
        b"!<arch>\n"
        + ar_member("debian-binary", b"2.0\n")
        + ar_member("control.tar.zst", b"control")
        + ar_member("data.tar.zst", b"data")
    )
    held = MODULE.snapshot(
        unsigned, "zstd unsigned package", MODULE.MAX_PACKAGE_BYTES
    )
    unsigned_receipt = tmp_path / "unsigned-export.json"
    unsigned_receipt.write_text("{}\n", encoding="utf-8")
    decoded: list[str] = []
    monkeypatch.setattr(
        MODULE,
        "_decode_secret_environment",
        lambda name, *_: decoded.append(name) or b"secret",
    )
    args = SimpleNamespace(
        input_package=unsigned,
        output_package=tmp_path / "signed" / MODULE.ARTIFACT_FILE_NAME,
        unsigned_export_receipt=unsigned_receipt,
        signed_export_receipt=tmp_path / "signed-export.json",
        transaction_manifest=(
            tmp_path / MODULE.TRANSACTION_MANIFEST_FILE_NAME
        ),
        receipt=tmp_path / MODULE.SIGNING_RECEIPT_FILE_NAME,
        policy=tmp_path / MODULE.POLICY_FILE_NAME,
        public_keyring=tmp_path / MODULE.KEYRING_FILE_NAME,
        release_version=RELEASE_VERSION,
        expected_fingerprint=FINGERPRINT,
        expected_public_keyring_sha256="a" * 64,
        expected_unsigned_package_sha256=held.sha256,
        expected_unsigned_package_size=str(held.size_bytes),
        expected_unsigned_export_receipt_sha256=sha256(unsigned_receipt),
        artifact_member_path=f"files/{MODULE.ARTIFACT_FILE_NAME}",
        signing_receipt_member_path=(
            f"signing/{MODULE.SIGNING_RECEIPT_FILE_NAME}"
        ),
        policy_member_path=(
            f"signing/policies/{LONG_KEY_ID}/{MODULE.POLICY_FILE_NAME}"
        ),
        public_keyring_member_path=(
            f"signing/keyrings/{LONG_KEY_ID}/{MODULE.KEYRING_FILE_NAME}"
        ),
        source_repository=MODULE.REPOSITORY,
        source_workflow=MODULE.WORKFLOW,
        source_run_id="123",
        source_run_attempt="1",
        source_ref=MODULE.REF,
        source_sha=SOURCE_SHA,
        source_actor="release-operator",
    )

    args.expected_unsigned_package_sha256 = "c" * 64
    with pytest.raises(MODULE.ContractError, match="external authority"):
        MODULE._sign(args)
    assert decoded == []
    args.expected_unsigned_package_sha256 = held.sha256
    with pytest.raises(MODULE.ContractError, match="cannot sign zstd"):
        MODULE._sign(args)
    assert decoded == []

    valid_control = lzma.compress(
        b"control tar fixture", format=lzma.FORMAT_XZ
    )
    corrupt_data = bytearray(
        lzma.compress(b"data tar fixture", format=lzma.FORMAT_XZ)
    )
    corrupt_data[-8] ^= 0x01
    unsigned.write_bytes(
        b"!<arch>\n"
        + ar_member("debian-binary", b"2.0\n")
        + ar_member("control.tar.xz", valid_control)
        + ar_member("data.tar.xz", bytes(corrupt_data))
    )
    corrupted = MODULE.snapshot(
        unsigned, "corrupt xz unsigned package", MODULE.MAX_PACKAGE_BYTES
    )
    args.expected_unsigned_package_sha256 = corrupted.sha256
    args.expected_unsigned_package_size = str(corrupted.size_bytes)
    with pytest.raises(MODULE.ContractError, match="XZ integrity"):
        MODULE._sign(args)
    assert decoded == []


def test_debian_metadata_and_exact_format_member_fail_closed(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    malformed = tmp_path / "malformed.deb"
    malformed.write_bytes(
        b"!<arch>\n"
        + ar_member("debian-binary", b"2.1\n")
        + ar_member("control.tar.xz", b"control")
        + ar_member("data.tar.xz", b"data")
    )
    with pytest.raises(MODULE.ContractError, match="exact format 2.0"):
        MODULE.require_unsigned_deb(malformed)

    package = {
        "architecture": "amd64",
        "name": "chummer6-avalonia",
        "version": MODULE.normalize_debian_version(RELEASE_VERSION),
    }

    def fake_run_tool(
        command: list[str], **_: object
    ) -> subprocess.CompletedProcess[bytes]:
        value = b""
        if command[-1] == "Architecture":
            value = b"amd64\n"
        elif command[-1] == "Package":
            value = b"chummer6-avalonia\n"
        elif command[-1] == "Version":
            value = b"0~replayed-version\n"
        return subprocess.CompletedProcess(command, 0, value, b"")

    monkeypatch.setattr(MODULE, "run_tool", fake_run_tool)
    with pytest.raises(MODULE.ContractError, match="Version differs"):
        MODULE.validate_debian_metadata(
            malformed, package, RELEASE_VERSION
        )


def test_private_output_creation_rejects_symlink_targets_and_parents(
    tmp_path: Path,
) -> None:
    victim = tmp_path / "victim"
    victim.write_bytes(b"unchanged")
    linked = tmp_path / "linked-output"
    linked.symlink_to(victim)
    with pytest.raises(MODULE.ContractError, match="new private regular file"):
        MODULE.write_new_bytes(linked, b"replacement", "linked output")
    assert victim.read_bytes() == b"unchanged"

    real_parent = tmp_path / "real-parent"
    real_parent.mkdir()
    linked_parent = tmp_path / "linked-parent"
    linked_parent.symlink_to(real_parent, target_is_directory=True)
    with pytest.raises(MODULE.ContractError, match="traversed safely"):
        MODULE.write_new_bytes(
            linked_parent / "output", b"replacement", "linked parent output"
        )
    assert list(real_parent.iterdir()) == []


@pytest.mark.parametrize("operation", ["write", "copy"])
def test_private_output_detects_injected_post_write_replacement(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    operation: str,
) -> None:
    source_path = tmp_path / "copy-source"
    source_path.write_bytes(b"governed source bytes")
    source = MODULE.snapshot(
        source_path, "copy source", MODULE.MAX_PACKAGE_BYTES
    )
    target = tmp_path / f"{operation}-target"

    def replace_output(
        _absolute: Path,
        parent_descriptor: int,
        basename: str,
        _output_descriptor: int,
    ) -> None:
        os.unlink(basename, dir_fd=parent_descriptor)
        attacker = os.open(
            basename,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL,
            0o600,
            dir_fd=parent_descriptor,
        )
        try:
            os.write(attacker, b"attacker replacement")
            os.fsync(attacker)
        finally:
            os.close(attacker)

    monkeypatch.setattr(
        MODULE, "_post_private_output_write", replace_output
    )
    with pytest.raises(
        MODULE.ContractError, match="output link or parent changed"
    ):
        if operation == "write":
            MODULE.write_new_bytes(
                target, b"governed output bytes", "governed output"
            )
        else:
            MODULE.copy_new(source, target, "governed copy")
    assert target.read_bytes() == b"attacker replacement"


def test_private_output_detects_injected_post_snapshot_mutation(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    target = tmp_path / "mutated-target"

    def mutate_output(
        _absolute: Path,
        _parent_descriptor: int,
        _basename: str,
        output_descriptor: int,
    ) -> None:
        os.pwrite(output_descriptor, b"X", 0)
        os.fsync(output_descriptor)

    monkeypatch.setattr(
        MODULE, "_post_private_output_write", mutate_output
    )
    with pytest.raises(MODULE.ContractError, match="changed"):
        MODULE.write_new_bytes(
            target, b"governed output bytes", "governed output"
        )
    assert target.read_bytes() == b"Xoverned output bytes"


def test_private_output_detects_injected_parent_path_replacement(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    output_parent = tmp_path / "governed-parent"
    output_parent.mkdir(mode=0o700)
    target = output_parent / "receipt.json"
    moved_parent = tmp_path / "detached-governed-parent"

    def replace_parent(
        absolute: Path,
        _parent_descriptor: int,
        basename: str,
        _output_descriptor: int,
    ) -> None:
        absolute.parent.rename(moved_parent)
        absolute.parent.mkdir(mode=0o700)
        (absolute.parent / basename).write_bytes(b"attacker replacement")

    monkeypatch.setattr(
        MODULE, "_post_private_output_write", replace_parent
    )
    with pytest.raises(
        MODULE.ContractError, match="output link or parent changed"
    ):
        MODULE.write_new_bytes(
            target, b"governed receipt", "governed receipt"
        )
    assert target.read_bytes() == b"attacker replacement"
    assert (moved_parent / target.name).read_bytes() == b"governed receipt"


def test_private_output_rejects_insecure_existing_parent(
    tmp_path: Path,
) -> None:
    unsafe_parent = tmp_path / "unsafe-parent"
    unsafe_parent.mkdir(mode=0o700)
    unsafe_parent.chmod(0o777)
    try:
        with pytest.raises(
            MODULE.ContractError, match="caller/root-owned"
        ):
            MODULE.write_new_bytes(
                unsafe_parent / "output",
                b"governed output",
                "insecure-parent output",
            )
    finally:
        unsafe_parent.chmod(0o700)


def test_private_output_rejects_wrong_owner_parent_metadata(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    metadata = tmp_path.stat()
    effective_uid = metadata.st_uid
    wrong_uid = effective_uid + 1
    if wrong_uid == 0:
        wrong_uid += 1
    monkeypatch.setattr(MODULE.os, "geteuid", lambda: effective_uid)
    wrong_owner = SimpleNamespace(
        st_mode=metadata.st_mode,
        st_uid=wrong_uid,
    )
    with pytest.raises(MODULE.ContractError, match="caller/root-owned"):
        MODULE._require_secure_parent(
            wrong_owner, "wrong-owner output parent"
        )


def test_private_copy_rejects_source_path_swap_before_open(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    source_path = tmp_path / "governed-source"
    governed_bytes = b"governed source bytes"
    source_path.write_bytes(governed_bytes)
    held = MODULE.snapshot(
        source_path, "governed source", MODULE.MAX_PACKAGE_BYTES
    )
    detached = tmp_path / "detached-governed-source"

    def swap_source(source: MODULE.Snapshot) -> None:
        source.path.rename(detached)
        source.path.write_bytes(governed_bytes)

    monkeypatch.setattr(MODULE, "_pre_private_input_copy", swap_source)
    destination = tmp_path / "signed-byte-copy"
    with pytest.raises(
        MODULE.ContractError, match="input changed before safe copying"
    ):
        MODULE.copy_new(held, destination, "signed-byte copy")
    assert destination.exists() is False
    assert detached.read_bytes() == governed_bytes
    assert source_path.read_bytes() == governed_bytes


@pytest.mark.parametrize("swapped_input", ["package", "receipt"])
def test_signing_stages_authenticated_inputs_before_private_validation(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    swapped_input: str,
) -> None:
    unsigned = tmp_path / MODULE.ARTIFACT_FILE_NAME
    unsigned.write_bytes(deb_bytes(signed=False))
    unsigned_receipt = tmp_path / "unsigned-export.json"
    unsigned_receipt.write_text("{}\n", encoding="utf-8")
    held_unsigned = MODULE.snapshot(
        unsigned, "external unsigned", MODULE.MAX_PACKAGE_BYTES
    )
    held_receipt = MODULE.snapshot(
        unsigned_receipt, "external receipt", MODULE.MAX_JSON_BYTES
    )
    detached = tmp_path / f"detached-{swapped_input}"
    observed: dict[str, Path] = {}

    def fake_private(
        private_args: object,
    ) -> tuple[dict[str, object], dict[str, MODULE.Snapshot]]:
        staged_package = Path(private_args.input_package)
        staged_receipt = Path(private_args.unsigned_export_receipt)
        observed["package"] = staged_package
        observed["receipt"] = staged_receipt
        assert staged_package != unsigned
        assert staged_receipt != unsigned_receipt
        assert staged_package.read_bytes() == unsigned.read_bytes()
        assert staged_receipt.read_bytes() == unsigned_receipt.read_bytes()
        assert staged_package.stat().st_mode & 0o077 == 0
        attacked = (
            unsigned if swapped_input == "package" else unsigned_receipt
        )
        attacked.rename(detached)
        attacked.write_bytes(b"attacker validator bytes")
        attacked.unlink()
        detached.chmod(0o400)
        detached.chmod(0o644)
        detached.rename(attacked)
        return {}, {}

    monkeypatch.setattr(MODULE, "_sign_private", fake_private)
    monkeypatch.setenv("RUNNER_TEMP", str(tmp_path))
    with pytest.raises(MODULE.ContractError, match="changed across"):
        MODULE._sign(
            SimpleNamespace(
                input_package=unsigned,
                unsigned_export_receipt=unsigned_receipt,
                expected_unsigned_package_sha256=held_unsigned.sha256,
                expected_unsigned_package_size=str(
                    held_unsigned.size_bytes
                ),
                expected_unsigned_export_receipt_sha256=(
                    held_receipt.sha256
                ),
            )
        )

    assert observed["package"] != unsigned
    assert observed["receipt"] != unsigned_receipt
    assert unsigned.read_bytes() == deb_bytes(signed=False)
    assert unsigned_receipt.read_text(encoding="utf-8") == "{}\n"
    assert not list(tmp_path.glob("chummer-linux-sign-input-*"))


@pytest.mark.parametrize("attack", ["output", "parent"])
def test_commit_last_transaction_detects_injected_change(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    attack: str,
) -> None:
    names = {
        "package": ("package", MODULE.MAX_PACKAGE_BYTES),
        "policy": ("policy", MODULE.MAX_JSON_BYTES),
        "public_keyring": ("keyring", MODULE.MAX_KEY_BYTES),
        "receipt": ("receipt", MODULE.MAX_JSON_BYTES),
        "signed_export_receipt": ("signed-export", MODULE.MAX_JSON_BYTES),
    }
    outputs = {
        key: MODULE.write_new_bytes(
            tmp_path / key / file_name,
            f"governed-{key}".encode("utf-8"),
            key,
        )
        for key, (file_name, _maximum) in names.items()
    }
    observed: set[str] = set()

    def attack_after_manifest_commit(
        first: dict[str, MODULE.Snapshot],
        _manifest: MODULE.Snapshot,
    ) -> None:
        observed.update(first)
        if attack == "output":
            first["signingReceipt"].path.write_bytes(
                b"attacker receipt bytes"
            )
        else:
            first["policy"].path.parent.chmod(0o750)

    monkeypatch.setattr(
        MODULE,
        "_post_output_transaction_commit",
        attack_after_manifest_commit,
    )
    try:
        with pytest.raises(MODULE.ContractError, match="changed"):
            MODULE.commit_output_transaction(
                outputs={
                    key: (outputs[key], key, maximum)
                    for key, (_file_name, maximum) in names.items()
                },
                manifest_path=(
                    tmp_path / MODULE.TRANSACTION_MANIFEST_FILE_NAME
                ),
                members={
                    "package": f"files/{MODULE.ARTIFACT_FILE_NAME}",
                    "policy": (
                        f"signing/policies/{LONG_KEY_ID}/"
                        f"{MODULE.POLICY_FILE_NAME}"
                    ),
                    "publicKeyring": (
                        f"signing/keyrings/{LONG_KEY_ID}/"
                        f"{MODULE.KEYRING_FILE_NAME}"
                    ),
                    "signingReceipt": (
                        f"signing/{MODULE.SIGNING_RECEIPT_FILE_NAME}"
                    ),
                    "signedExportReceipt": (
                        MODULE.SIGNED_EXPORT_RECEIPT_FILE_NAME
                    ),
                },
            )
    finally:
        outputs["policy"].path.parent.chmod(0o700)
    assert observed == MODULE.TRANSACTION_OUTPUT_KEYS


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("contractVersion", True),
        ("contractVersion", 1.0),
        ("sizeBytes", 1.0),
    ],
)
def test_transaction_manifest_rejects_integer_type_confusion(
    tmp_path: Path,
    field: str,
    value: object,
) -> None:
    _args, outputs, _manifest = transaction_fixture(tmp_path)
    members = MODULE._canonical_transaction_members(FINGERPRINT)
    payload = MODULE._transaction_payload(
        outputs=outputs,
        members=members,
    )
    if field == "contractVersion":
        payload[field] = value
    else:
        payload["outputs"]["package"][field] = float(
            payload["outputs"]["package"][field]
        )
    with pytest.raises(
        MODULE.ContractError,
        match="differs from all five",
    ):
        MODULE.validate_transaction_manifest(
            payload,
            outputs=outputs,
            members=members,
        )


def test_output_set_stages_exact_manifest_bound_tree_atomically(
    tmp_path: Path,
) -> None:
    args, sources, manifest = transaction_fixture(tmp_path / "source")
    args.output_root = tmp_path / "published"

    result = MODULE._stage_output_set(args)

    assert Path(result["publishedRoot"]) == args.output_root
    assert result["transactionManifestSha256"] == manifest.sha256
    members = MODULE._canonical_transaction_members(FINGERPRINT)
    assert MODULE._tree_regular_members(args.output_root) == (
        set(members.values()) | {MODULE.TRANSACTION_MANIFEST_FILE_NAME}
    )
    for key, member in members.items():
        published = MODULE.snapshot(
            args.output_root.joinpath(*Path(member).parts),
            f"published {key}",
            MODULE.MAX_PACKAGE_BYTES,
        )
        assert published.sha256 == sources[key].sha256


def test_output_set_rehash_rejects_mutation_at_atomic_publish_boundary(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    args, _sources, _manifest = transaction_fixture(tmp_path / "source")
    args.output_root = tmp_path / "published"
    original_rename = MODULE._rename_directory_noreplace
    package_member = MODULE._canonical_transaction_members(FINGERPRINT)[
        "package"
    ]

    def mutate_then_publish(
        parent_descriptor: int,
        source_name: str,
        destination_name: str,
    ) -> None:
        staged_package = args.output_root.parent.joinpath(
            source_name, *Path(package_member).parts
        )
        staged_package.write_bytes(b"late attacker package mutation")
        original_rename(
            parent_descriptor, source_name, destination_name
        )

    monkeypatch.setattr(
        MODULE, "_rename_directory_noreplace", mutate_then_publish
    )

    with pytest.raises(MODULE.ContractError, match="differs"):
        MODULE._stage_output_set(args)


def test_output_set_atomic_publish_never_overwrites_existing_root(
    tmp_path: Path,
) -> None:
    args, _sources, _manifest = transaction_fixture(tmp_path / "source")
    args.output_root = tmp_path / "published"
    args.output_root.mkdir()
    sentinel = args.output_root / "operator-owned"
    sentinel.write_bytes(b"preserve")

    with pytest.raises(MODULE.ContractError, match="already exists"):
        MODULE._stage_output_set(args)

    assert sentinel.read_bytes() == b"preserve"


def test_signed_package_preserves_exact_unsigned_prefix(
    tmp_path: Path,
) -> None:
    unsigned_path = tmp_path / "unsigned.deb"
    signed_path = tmp_path / "signed.deb"
    unsigned_path.write_bytes(deb_bytes(signed=False))
    signed_path.write_bytes(deb_bytes(signed=True))
    unsigned = MODULE.snapshot(
        unsigned_path, "unsigned package", MODULE.MAX_PACKAGE_BYTES
    )
    signed = MODULE.snapshot(
        signed_path, "signed package", MODULE.MAX_PACKAGE_BYTES
    )
    MODULE.require_signed_prefix_matches_unsigned(unsigned, signed)

    changed = bytearray(signed_path.read_bytes())
    changed[80] ^= 0x01
    signed_path.write_bytes(bytes(changed))
    tampered = MODULE.snapshot(
        signed_path, "tampered signed package", MODULE.MAX_PACKAGE_BYTES
    )
    with pytest.raises(
        MODULE.ContractError, match="authenticated unsigned package prefix"
    ):
        MODULE.require_signed_prefix_matches_unsigned(unsigned, tampered)


def test_tamper_copy_changes_authenticated_data_not_archive_structure(
    tmp_path: Path,
) -> None:
    source = tmp_path / MODULE.ARTIFACT_FILE_NAME
    target = tmp_path / "tampered.deb"
    source.write_bytes(deb_bytes(signed=True))
    before_members = MODULE._ar_members(source)

    result = MODULE.tampered_copy(source, target)

    assert result.sha256 != sha256(source)
    assert MODULE._ar_members(target) == before_members
    before_payload = tmp_path / "before-payload"
    before_signature = tmp_path / "before-signature"
    after_payload = tmp_path / "after-payload"
    after_signature = tmp_path / "after-signature"
    MODULE.extract_signed_payload_and_signature(
        source, before_payload, before_signature
    )
    MODULE.extract_signed_payload_and_signature(
        target, after_payload, after_signature
    )
    assert sha256(after_payload) != sha256(before_payload)
    assert sha256(after_signature) == sha256(before_signature)


def colon_key_listing(
    *, secret: bool, signing_subkey: bool = False, expired: bool = False
) -> bytes:
    primary = "sec" if secret else "pub"
    secondary = "ssb" if secret else "sub"
    validity = "e" if expired else "u"
    rows = [
        ":".join(
            [
                primary,
                validity,
                "3072",
                "1",
                LONG_KEY_ID,
                "1700000000",
                "",
                "",
                "",
                "",
                "",
                "scESC",
                "",
            ]
        ),
        ":".join(["fpr", "", "", "", "", "", "", "", "", FINGERPRINT, "", ""]),
    ]
    if signing_subkey:
        rows.extend(
            [
                ":".join(
                    [
                        secondary,
                        "u",
                        "3072",
                        "1",
                        "A" * 16,
                        "1700000000",
                        "",
                        "",
                        "",
                        "",
                        "",
                        "s",
                        "",
                    ]
                ),
                ":".join(
                    ["fpr", "", "", "", "", "", "", "", "", "B" * 40, "", ""]
                ),
            ]
        )
    return ("\n".join(rows) + "\n").encode("utf-8")


def test_primary_key_inventory_rejects_expiry_and_signing_subkey_ambiguity() -> None:
    _, _, primary, secondaries = MODULE._parse_key_inventory(
        colon_key_listing(secret=True), secret=True
    )
    MODULE._require_usable_primary_key(primary, secondaries, FINGERPRINT)

    _, _, primary, secondaries = MODULE._parse_key_inventory(
        colon_key_listing(secret=True, expired=True), secret=True
    )
    with pytest.raises(MODULE.ContractError, match="expired"):
        MODULE._require_usable_primary_key(
            primary, secondaries, FINGERPRINT
        )

    _, _, primary, secondaries = MODULE._parse_key_inventory(
        colon_key_listing(secret=True, signing_subkey=True), secret=True
    )
    with pytest.raises(MODULE.ContractError, match="signing subkey"):
        MODULE._require_usable_primary_key(
            primary, secondaries, FINGERPRINT
        )


def test_verify_crypto_requires_exact_exit_13_tamper_rejection(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    package, policy, keyring = fixture_files(tmp_path)
    exits = iter([0, 13])

    def fake_run_tool(*args: object, **kwargs: object) -> object:
        return subprocess.CompletedProcess(
            args=args,
            returncode=next(exits),
            stdout=b"",
            stderr=b"",
        )

    created = int(datetime.now(UTC).timestamp())
    openpgp = {
        "createdAt": datetime.fromtimestamp(created, UTC)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z"),
        "creationTimestamp": created,
        "fingerprint": FINGERPRINT,
        "hashAlgorithm": "sha256",
        "primaryFingerprint": FINGERPRINT,
        "publicKeyAlgorithm": "rsa",
    }
    monkeypatch.setattr(MODULE, "run_tool", fake_run_tool)
    monkeypatch.setattr(
        MODULE, "verify_openpgp_signature", lambda **_: openpgp
    )

    result = MODULE.verify_crypto(
        package=package.path,
        policy=policy.path,
        keyring=keyring.path,
        signing_fingerprint=FINGERPRINT,
        temporary_root=tmp_path,
    )

    assert result["positiveExitCode"] == 0
    assert result["tamperNegative"]["observedExitCode"] == 13
    assert result["openPgpSignature"] == openpgp


def test_keyless_verifier_requires_independent_key_pins(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    package, policy, keyring = fixture_files(tmp_path)
    receipt_path = tmp_path / MODULE.SIGNING_RECEIPT_FILE_NAME
    receipt_path.write_text(
        __import__("json").dumps(receipt(package, policy, keyring)),
        encoding="utf-8",
    )
    signed_export_path = tmp_path / MODULE.SIGNED_EXPORT_RECEIPT_FILE_NAME
    signed_export_path.write_text("{}\n", encoding="utf-8")
    transaction_path = tmp_path / MODULE.TRANSACTION_MANIFEST_FILE_NAME
    transaction_path.write_text("{}\n", encoding="utf-8")
    args = SimpleNamespace(
        package=package.path,
        policy=policy.path,
        public_keyring=keyring.path,
        receipt=receipt_path,
        signed_export_receipt=signed_export_path,
        transaction_manifest=transaction_path,
        release_version=RELEASE_VERSION,
        expected_primary_fingerprint=FINGERPRINT,
        expected_public_keyring_sha256="f" * 64,
        expected_signed_export_receipt_sha256="e" * 64,
        expected_transaction_manifest_sha256="d" * 64,
    )

    with pytest.raises(MODULE.ContractError, match="independent lifecycle"):
        MODULE._verify(args)


@pytest.mark.parametrize("swap_mode", ["mutate", "replace-path"])
def test_keyless_verifier_uses_private_copies_and_detects_input_swap(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    swap_mode: str,
) -> None:
    package, policy, keyring = fixture_files(tmp_path)
    receipt_path = tmp_path / MODULE.SIGNING_RECEIPT_FILE_NAME
    receipt_path.write_text("{}\n", encoding="utf-8")
    signed_export_path = tmp_path / MODULE.SIGNED_EXPORT_RECEIPT_FILE_NAME
    signed_export_path.write_text("{}\n", encoding="utf-8")
    transaction_path = tmp_path / MODULE.TRANSACTION_MANIFEST_FILE_NAME
    transaction_path.write_text("{}\n", encoding="utf-8")
    original_bytes = package.path.read_bytes()

    def fake_verify(
        private_args: object, private_package: MODULE.Snapshot
    ) -> dict[str, object]:
        private_paths = {
            private_package.path,
            private_args.policy,
            private_args.public_keyring,
            private_args.receipt,
            private_args.signed_export_receipt,
            private_args.transaction_manifest,
        }
        assert all(tmp_path not in path.parents for path in private_paths)
        if swap_mode == "mutate":
            package.path.write_bytes(
                bytes([original_bytes[0] ^ 1]) + original_bytes[1:]
            )
        else:
            package.path.rename(tmp_path / "detached-signed-package.deb")
            package.path.write_bytes(original_bytes)
        return {"privateCopiesVerified": True}

    monkeypatch.setattr(MODULE, "_verify_held", fake_verify)
    with pytest.raises(
        MODULE.ContractError, match="package changed during keyless verification"
    ):
        MODULE._verify(
            SimpleNamespace(
                package=package.path,
                policy=policy.path,
                public_keyring=keyring.path,
                receipt=receipt_path,
                signed_export_receipt=signed_export_path,
                transaction_manifest=transaction_path,
                release_version=RELEASE_VERSION,
                expected_primary_fingerprint=FINGERPRINT,
                expected_public_keyring_sha256=keyring.sha256,
                expected_signed_export_receipt_sha256=sha256(
                    signed_export_path
                ),
                expected_transaction_manifest_sha256=sha256(
                    transaction_path
                ),
            )
        )


@pytest.mark.parametrize(
    "input_name",
    ["package", "policy", "public_keyring"],
)
@pytest.mark.parametrize("swap_mode", ["mutate", "replace-path"])
def test_keyless_verifier_rechecks_private_crypto_inputs(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    input_name: str,
    swap_mode: str,
) -> None:
    package, policy, keyring = fixture_files(tmp_path)
    receipt_path = tmp_path / MODULE.SIGNING_RECEIPT_FILE_NAME
    receipt_path.write_text("{}\n", encoding="utf-8")
    signed_export_path = tmp_path / MODULE.SIGNED_EXPORT_RECEIPT_FILE_NAME
    signed_export_path.write_text("{}\n", encoding="utf-8")
    transaction_path = tmp_path / MODULE.TRANSACTION_MANIFEST_FILE_NAME
    transaction_path.write_text("{}\n", encoding="utf-8")

    def fake_verify(
        private_args: object, private_package: MODULE.Snapshot
    ) -> dict[str, object]:
        target = (
            private_package.path
            if input_name == "package"
            else Path(getattr(private_args, input_name))
        )
        original = target.read_bytes()
        if swap_mode == "mutate":
            target.write_bytes(bytes([original[0] ^ 1]) + original[1:])
        else:
            target.rename(target.with_name(f"{target.name}.detached"))
            target.write_bytes(original)
            target.chmod(0o600)
        return {"privateCopiesVerified": True}

    monkeypatch.setattr(MODULE, "_verify_held", fake_verify)
    with pytest.raises(
        MODULE.ContractError,
        match=f"private verification {input_name.replace('_', ' ')} changed",
    ):
        MODULE._verify(
            SimpleNamespace(
                package=package.path,
                policy=policy.path,
                public_keyring=keyring.path,
                receipt=receipt_path,
                signed_export_receipt=signed_export_path,
                transaction_manifest=transaction_path,
                release_version=RELEASE_VERSION,
                expected_primary_fingerprint=FINGERPRINT,
                expected_public_keyring_sha256=keyring.sha256,
                expected_signed_export_receipt_sha256=sha256(
                    signed_export_path
                ),
                expected_transaction_manifest_sha256=sha256(
                    transaction_path
                ),
            )
        )


@pytest.mark.parametrize("input_name", ["candidate", "n_minus_one"])
@pytest.mark.parametrize("swap_mode", ["mutate", "replace-path"])
def test_lifecycle_protected_stage_rejects_copy_fault(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    input_name: str,
    swap_mode: str,
) -> None:
    candidate = tmp_path / "candidate.deb"
    previous = tmp_path / "previous.deb"
    candidate.write_bytes(b"authenticated-candidate")
    previous.write_bytes(b"authenticated-n-minus-one")
    temporary_parent = tmp_path / "var-tmp"
    temporary_parent.mkdir()

    def inject(staged: dict[str, MODULE.Snapshot]) -> None:
        target = staged[input_name].path
        original = target.read_bytes()
        if swap_mode == "mutate":
            target.write_bytes(bytes([original[0] ^ 1]) + original[1:])
        else:
            target.rename(target.with_name(f"{target.name}.detached"))
            target.write_bytes(original)
            target.chmod(0o600)

    monkeypatch.setattr(MODULE, "_post_lifecycle_package_stage", inject)
    with pytest.raises(
        MODULE.ContractError,
        match=f"protected lifecycle {input_name.replace('_', ' ')} package changed",
    ):
        MODULE._stage_lifecycle_packages_for_current_user(
            SimpleNamespace(
                candidate=candidate,
                expected_candidate_sha256=sha256(candidate),
                expected_candidate_size=str(candidate.stat().st_size),
                n_minus_one=previous,
                expected_n_minus_one_sha256=sha256(previous),
                expected_n_minus_one_size=str(previous.stat().st_size),
            ),
            temporary_parent=temporary_parent,
        )
    assert not tuple(temporary_parent.iterdir())


def test_lifecycle_protected_stage_copies_authenticated_packages(
    tmp_path: Path,
) -> None:
    candidate = tmp_path / "candidate.deb"
    previous = tmp_path / "previous.deb"
    candidate_bytes = b"authenticated-candidate"
    previous_bytes = b"authenticated-n-minus-one"
    candidate.write_bytes(candidate_bytes)
    previous.write_bytes(previous_bytes)
    temporary_parent = tmp_path / "var-tmp"
    temporary_parent.mkdir()
    result = MODULE._stage_lifecycle_packages_for_current_user(
        SimpleNamespace(
            candidate=candidate,
            expected_candidate_sha256=sha256(candidate),
            expected_candidate_size=str(candidate.stat().st_size),
            n_minus_one=previous,
            expected_n_minus_one_sha256=sha256(previous),
            expected_n_minus_one_size=str(previous.stat().st_size),
        ),
        temporary_parent=temporary_parent,
    )
    protected_root = Path(result["protectedRoot"])
    try:
        assert protected_root.stat().st_mode & 0o777 == 0o700
        assert Path(result["candidatePath"]).read_bytes() == candidate_bytes
        assert Path(result["nMinusOnePath"]).read_bytes() == previous_bytes
        candidate.write_bytes(b"mutated-after-stage")
        previous.write_bytes(b"mutated-after-stage")
        assert Path(result["candidatePath"]).read_bytes() == candidate_bytes
        assert Path(result["nMinusOnePath"]).read_bytes() == previous_bytes
    finally:
        shutil.rmtree(protected_root)


def test_lifecycle_protected_stage_requires_root(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(MODULE.os, "geteuid", lambda: 1000)
    with pytest.raises(MODULE.ContractError, match="requires root"):
        MODULE._stage_lifecycle_packages(SimpleNamespace())


def test_secret_environment_is_not_forwarded_to_child_tools(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkey_environment = {
        "CHUMMER_LINUX_DEB_SIGNING_PRIVATE_KEY_B64": "secret",
        "CHUMMER_LINUX_DEB_SIGNING_PASSPHRASE_B64": "secret",
    }
    environment = MODULE._gpg_environment(tmp_path)

    assert set(environment) == {"GNUPGHOME", "HOME", "LANG", "LC_ALL", "PATH"}
    assert set(monkey_environment).isdisjoint(environment)
    help_text = MODULE.parser().format_help()
    assert "--private-key-env" not in help_text
    assert "--passphrase-env" not in help_text

    for name, value in {
        **monkey_environment,
        "AWS_SECRET_ACCESS_KEY": "poisoned",
        "LD_PRELOAD": "/poisoned/library.so",
        "PYTHONPATH": "/poisoned/python",
    }.items():
        monkeypatch.setenv(name, value)
    observed = MODULE.run_tool(
        ["/usr/bin/env"], label="actual child environment inspection"
    )
    child_environment = dict(
        line.split("=", 1)
        for line in observed.stdout.decode("utf-8").splitlines()
    )
    assert child_environment == MODULE.MINIMAL_TOOL_ENVIRONMENT


def test_dpkg_query_receives_only_minimal_environment_while_secrets_exist(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    observed: list[dict[str, str]] = []

    def fake_run(
        command: list[str], **kwargs: object
    ) -> subprocess.CompletedProcess[bytes]:
        assert command[0] == "/usr/bin/dpkg-query"
        observed.append(dict(kwargs["env"]))
        return subprocess.CompletedProcess(
            args=command,
            returncode=0,
            stdout=b"installed\n0.29\n",
            stderr=b"",
        )

    monkeypatch.setenv(MODULE.PRIVATE_KEY_ENV, "exposed-private-key")
    monkeypatch.setenv(MODULE.PASSPHRASE_ENV, "exposed-passphrase")
    monkeypatch.setattr(MODULE.subprocess, "run", fake_run)

    assert MODULE._dpkg_package_version("debsig-verify") == "0.29"
    assert observed == [MODULE.MINIMAL_TOOL_ENVIRONMENT]


def test_keyless_verifier_rejects_corrupt_signed_xz_payload(
    tmp_path: Path,
) -> None:
    valid_control = lzma.compress(
        b"control tar fixture", format=lzma.FORMAT_XZ
    )
    corrupt_data = bytearray(
        lzma.compress(b"data tar fixture", format=lzma.FORMAT_XZ)
    )
    corrupt_data[-8] ^= 0x01
    package_path = tmp_path / MODULE.ARTIFACT_FILE_NAME
    package_path.write_bytes(
        b"!<arch>\n"
        + ar_member("debian-binary", b"2.0\n")
        + ar_member("control.tar.xz", valid_control)
        + ar_member("data.tar.xz", bytes(corrupt_data))
        + ar_member("_gpgorigin", b"legitimate-signature-placeholder")
    )
    package = MODULE.snapshot(
        package_path,
        "corrupt signed package",
        MODULE.MAX_PACKAGE_BYTES,
    )

    with pytest.raises(MODULE.ContractError, match="XZ integrity"):
        MODULE._verify_held(SimpleNamespace(), package)


def test_agent_stop_rejects_replacement_for_same_ephemeral_home(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    original = MODULE.AgentProcess(
        pid=111111,
        start_time_ticks=1,
        socket_path=tmp_path / "S.gpg-agent",
    )
    replacement = MODULE.AgentProcess(
        pid=222222,
        start_time_ticks=2,
        socket_path=original.socket_path,
    )
    monkeypatch.setattr(
        MODULE, "_wait_for_agent_exit", lambda *_args: True
    )
    monkeypatch.setattr(
        MODULE, "_discover_ephemeral_agent", lambda _home: replacement
    )

    assert MODULE._confirm_agent_stopped(tmp_path, original) is False


def test_post_cleanup_scope_rejects_live_replacement_agent(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    removed_home = tmp_path / "removed-home"
    monkeypatch.setattr(
        MODULE,
        "_ephemeral_agent_pids_for_home",
        lambda home: [222222] if home == removed_home else [],
    )

    with pytest.raises(
        MODULE.ContractError, match="replacement GnuPG agent remains"
    ):
        MODULE._require_ephemeral_agent_scope_absent(
            removed_home, removed_home / "S.gpg-agent"
        )


def test_ephemeral_home_exit_rejects_replacement_agent_after_cleanup(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    original: MODULE.AgentProcess | None = None
    replacement: MODULE.AgentProcess | None = None
    discoveries = 0

    def discover(home: Path) -> MODULE.AgentProcess:
        nonlocal original, replacement, discoveries
        discoveries += 1
        if original is None:
            original = MODULE.AgentProcess(
                111111, 1, home / "S.gpg-agent"
            )
            replacement = MODULE.AgentProcess(
                222222, 2, home / "S.gpg-agent"
            )
        assert replacement is not None
        return original if discoveries == 1 else replacement

    monkeypatch.setattr(MODULE, "_discover_ephemeral_agent", discover)
    monkeypatch.setattr(
        MODULE, "_wait_for_agent_exit", lambda *_args: True
    )
    monkeypatch.setattr(
        MODULE,
        "_last_resort_agent_shutdown",
        lambda _home, _process: True,
    )
    monkeypatch.setattr(
        MODULE,
        "_ephemeral_agent_pids_for_home",
        lambda _home: [222222],
    )
    monkeypatch.setattr(
        MODULE,
        "run_tool",
        lambda command, **_kwargs: subprocess.CompletedProcess(
            args=command, returncode=0, stdout=b"", stderr=b""
        ),
    )
    held_home: Path | None = None

    with pytest.raises(
        MODULE.ContractError, match="replacement GnuPG agent remains"
    ):
        with MODULE.EphemeralGpgHome(tmp_path) as home:
            held_home = home

    assert held_home is not None and not held_home.exists()
    assert discoveries >= 3


def test_ephemeral_gpg_home_kills_agent_and_removes_home_on_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    commands: list[list[str]] = []

    def fake_run_tool(
        command: object, **_: object
    ) -> subprocess.CompletedProcess[bytes]:
        commands.append(list(command))
        return subprocess.CompletedProcess(
            args=command, returncode=0, stdout=b"", stderr=b""
        )

    monkeypatch.setattr(MODULE, "run_tool", fake_run_tool)
    monkeypatch.setattr(
        MODULE, "_discover_ephemeral_agent", lambda _home: None
    )
    held_home: Path | None = None
    with pytest.raises(RuntimeError, match="injected signing failure"):
        with MODULE.EphemeralGpgHome(tmp_path) as home:
            held_home = home
            (home / "agent-started").write_text("fixture", encoding="utf-8")
            raise RuntimeError("injected signing failure")

    assert held_home is not None and not held_home.exists()
    assert len(commands) == 1
    assert commands[0][:3] == [
        "/usr/bin/gpgconf",
        "--homedir",
        str(held_home),
    ]
    assert commands[0][-2:] == ["--kill", "all"]


def test_ephemeral_gpg_home_uses_fallback_shutdown_and_removes_home(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    commands: list[list[str]] = []
    agent_live = True
    agent_socket: socket.socket | None = None

    def fake_run_tool(
        command: object, **_: object
    ) -> subprocess.CompletedProcess[bytes]:
        nonlocal agent_live, agent_socket
        parsed = list(command)
        commands.append(parsed)
        if parsed[0] == "/usr/bin/gpgconf":
            raise MODULE.ContractError("injected gpgconf failure")
        assert parsed[0] == "/usr/bin/gpg-connect-agent"
        agent_live = False
        if agent_socket is not None:
            agent_socket.close()
            agent_socket = None
        return subprocess.CompletedProcess(
            args=command, returncode=0, stdout=b"", stderr=b""
        )

    monkeypatch.setattr(MODULE, "run_tool", fake_run_tool)
    monkeypatch.setattr(
        MODULE, "_discover_ephemeral_agent", lambda _home: None
    )
    held_home: Path | None = None
    with pytest.raises(RuntimeError, match="injected signing failure"):
        with MODULE.EphemeralGpgHome(tmp_path) as home:
            held_home = home
            agent_socket = socket.socket(socket.AF_UNIX)
            agent_socket.bind(str(home / "S.gpg-agent"))
            raise RuntimeError("injected signing failure")

    assert held_home is not None and not held_home.exists()
    assert agent_live is False
    assert [command[0] for command in commands] == [
        "/usr/bin/gpgconf",
        "/usr/bin/gpg-connect-agent",
    ]
    assert commands[1][-2:] == ["KILLAGENT", "/bye"]
    assert not (held_home / "S.gpg-agent").exists()


def test_ephemeral_gpg_home_surfaces_double_shutdown_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def fake_run_tool(
        command: object, **_: object
    ) -> subprocess.CompletedProcess[bytes]:
        return subprocess.CompletedProcess(
            args=command, returncode=1, stdout=b"", stderr=b"injected"
        )

    process = MODULE.AgentProcess(
        pid=123456,
        start_time_ticks=789,
        socket_path=tmp_path / "S.gpg-agent",
    )
    forced: list[MODULE.AgentProcess] = []
    monkeypatch.setattr(MODULE, "run_tool", fake_run_tool)
    monkeypatch.setattr(
        MODULE, "_discover_ephemeral_agent", lambda _home: process
    )
    monkeypatch.setattr(
        MODULE,
        "_last_resort_agent_shutdown",
        lambda _home, value: forced.append(value) or True,
    )
    held_home: Path | None = None
    with pytest.raises(RuntimeError, match="injected signing failure") as caught:
        with MODULE.EphemeralGpgHome(tmp_path) as home:
            held_home = home
            raise RuntimeError("injected signing failure")

    assert held_home is not None and not held_home.exists()
    assert any(
        "last-resort termination confirmed" in note
        for note in getattr(caught.value, "__notes__", [])
    )
    assert forced == [process]


def test_ephemeral_gpg_home_double_shutdown_failure_on_normal_exit(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def fake_run_tool(
        command: object, **_: object
    ) -> subprocess.CompletedProcess[bytes]:
        return subprocess.CompletedProcess(
            args=command, returncode=1, stdout=b"", stderr=b"injected"
        )

    process = MODULE.AgentProcess(
        pid=123456,
        start_time_ticks=789,
        socket_path=tmp_path / "S.gpg-agent",
    )
    forced: list[MODULE.AgentProcess] = []
    monkeypatch.setattr(MODULE, "run_tool", fake_run_tool)
    monkeypatch.setattr(
        MODULE, "_discover_ephemeral_agent", lambda _home: process
    )
    monkeypatch.setattr(
        MODULE,
        "_last_resort_agent_shutdown",
        lambda _home, value: forced.append(value) or True,
    )
    held_home: Path | None = None
    with pytest.raises(
        MODULE.ContractError, match="last-resort termination confirmed"
    ):
        with MODULE.EphemeralGpgHome(tmp_path) as home:
            held_home = home
            (home / "S.gpg-agent").write_text("fixture", encoding="utf-8")

    assert held_home is not None and not held_home.exists()
    assert not (held_home / "S.gpg-agent").exists()
    assert forced == [process]


def test_ephemeral_gpg_home_last_resort_terminates_real_fixture_agent(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    original_run_tool = MODULE.run_tool
    discovered: list[MODULE.AgentProcess] = []
    original_discovery = MODULE._discover_ephemeral_agent

    def controlled_run_tool(
        command: object, **kwargs: object
    ) -> subprocess.CompletedProcess[bytes]:
        parsed = list(command)
        if (
            parsed[0] == "/usr/bin/gpgconf"
            and "--kill" in parsed
        ) or "KILLAGENT" in parsed:
            return subprocess.CompletedProcess(
                args=command,
                returncode=1,
                stdout=b"",
                stderr=b"injected shutdown failure",
            )
        return original_run_tool(parsed, **kwargs)

    held_home: Path | None = None
    with pytest.raises(RuntimeError, match="injected signing failure") as caught:
        with MODULE.EphemeralGpgHome(tmp_path) as home:
            held_home = home
            original_run_tool(
                [
                    "/usr/bin/gpg-connect-agent",
                    "--homedir",
                    str(home),
                    "GETINFO pid",
                    "/bye",
                ],
                label="fixture GnuPG agent startup",
                environment=MODULE._gpg_environment(home),
            )
            process = original_discovery(home)
            assert process is not None
            discovered.append(process)
            monkeypatch.setattr(MODULE, "run_tool", controlled_run_tool)
            monkeypatch.setattr(
                MODULE,
                "_discover_ephemeral_agent",
                lambda _home: process,
            )
            raise RuntimeError("injected signing failure")

    monkeypatch.setattr(
        MODULE, "_discover_ephemeral_agent", original_discovery
    )
    assert held_home is not None and not held_home.exists()
    assert len(discovered) == 1
    assert not MODULE._same_agent_process(held_home, discovered[0])
    assert any(
        "last-resort termination confirmed" in note
        for note in getattr(caught.value, "__notes__", [])
    )


@pytest.mark.parametrize("operation_fails", [False, True])
def test_ephemeral_gpg_home_surfaces_filesystem_cleanup_failure(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    operation_fails: bool,
) -> None:
    manager = MODULE.EphemeralGpgHome(tmp_path)
    original_cleanup = manager._temporary.cleanup

    def fake_run_tool(
        command: object, **_: object
    ) -> subprocess.CompletedProcess[bytes]:
        return subprocess.CompletedProcess(
            args=command, returncode=0, stdout=b"", stderr=b""
        )

    def fail_cleanup() -> None:
        raise OSError("injected private-home cleanup failure")

    monkeypatch.setattr(MODULE, "run_tool", fake_run_tool)
    monkeypatch.setattr(
        MODULE, "_discover_ephemeral_agent", lambda _home: None
    )
    monkeypatch.setattr(manager._temporary, "cleanup", fail_cleanup)
    held_home: Path | None = None
    try:
        if operation_fails:
            with pytest.raises(
                RuntimeError, match="injected signing failure"
            ) as caught:
                with manager as home:
                    held_home = home
                    raise RuntimeError("injected signing failure")
            assert any(
                "private-home cleanup failure" in note
                for note in getattr(caught.value, "__notes__", [])
            )
        else:
            with pytest.raises(
                MODULE.ContractError, match="private-home cleanup failure"
            ):
                with manager as home:
                    held_home = home
    finally:
        original_cleanup()
    assert held_home is not None and not held_home.exists()


@pytest.mark.skipif(
    not REAL_TOOLS_AVAILABLE,
    reason="exact Debian origin-signing tools are not installed",
)
def test_real_debsigs_029_round_trip_uses_only_ephemeral_fixture_key(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    request: pytest.FixtureRequest,
    record_property: Callable[[str, object], None],
) -> None:
    if EXTRACTED_TOOL_ROOT:
        tool_records = {
            "debsigs": tool_row("debsigs", "0.1.26"),
            "debsigVerify": tool_row("debsig-verify", "0.29"),
            "gpg": tool_row("gpg", "2.4.4-fixture"),
            "gpgv": tool_row("gpgv", "2.4.4-fixture"),
        }
        monkeypatch.setitem(
            MODULE.TOOLS,
            "debsigs",
            MODULE.Tool(
                "debsigs",
                real_tool_path("debsigs"),
                "debsigs",
                "0.1.26",
            ),
        )
        monkeypatch.setitem(
            MODULE.TOOLS,
            "debsigVerify",
            MODULE.Tool(
                "debsig-verify",
                real_tool_path("debsig-verify"),
                "debsig-verify",
                "0.29",
            ),
        )
        monkeypatch.setattr(
            MODULE,
            "collect_tool_records",
            lambda: copy.deepcopy(tool_records),
        )
        monkeypatch.setattr(
            MODULE,
            "tool_record",
            lambda tool: copy.deepcopy(
                tool_records[
                    "debsigVerify"
                    if tool.name == "debsig-verify"
                    else tool.name
                ]
            ),
        )
        original_gpg_environment = MODULE._gpg_environment

        def extracted_environment(home: Path) -> dict[str, str]:
            environment = original_gpg_environment(home)
            environment["PATH"] = (
                f"{Path(EXTRACTED_TOOL_ROOT) / 'usr' / 'bin'}:"
                "/usr/bin:/bin"
            )
            environment["PERL5LIB"] = str(
                Path(EXTRACTED_TOOL_ROOT) / "usr" / "share" / "perl5"
            )
            return environment

        monkeypatch.setattr(
            MODULE, "_gpg_environment", extracted_environment
        )

    generator_home = tmp_path / "fixture-key-home"
    generator_home.mkdir(mode=0o700)

    def cleanup_generator_home() -> None:
        subprocess.run(
            [
                "/usr/bin/gpgconf",
                "--homedir",
                str(generator_home),
                "--kill",
                "all",
            ],
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        if generator_home.exists():
            shutil.rmtree(generator_home)

    request.addfinalizer(cleanup_generator_home)
    gpg_environment = {
        "GNUPGHOME": str(generator_home),
        "HOME": str(generator_home),
        "LANG": "C.UTF-8",
        "LC_ALL": "C.UTF-8",
        "PATH": "/usr/bin:/bin",
    }
    passphrase = b"ephemeral-fixture-passphrase"
    subprocess.run(
        [
            "/usr/bin/gpg",
            "--batch",
            "--pinentry-mode",
            "loopback",
            "--passphrase-fd",
            "0",
            "--quick-generate-key",
            "Chummer ephemeral test <fixture@example.invalid>",
            "rsa3072",
            "sign",
            "1d",
        ],
        input=passphrase + b"\n",
        env=gpg_environment,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    listing = subprocess.run(
        [
            "/usr/bin/gpg",
            "--batch",
            "--with-colons",
            "--fingerprint",
            "--list-secret-keys",
        ],
        env=gpg_environment,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    ).stdout
    fingerprint, _, primary, secondaries = MODULE._parse_key_inventory(
        listing, secret=True
    )
    MODULE._require_usable_primary_key(primary, secondaries, fingerprint)
    exported_secret = subprocess.run(
        [
            "/usr/bin/gpg",
            "--batch",
            "--pinentry-mode",
            "loopback",
            "--passphrase-fd",
            "0",
            "--armor",
            "--export-secret-keys",
            fingerprint,
        ],
        input=passphrase + b"\n",
        env=gpg_environment,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    ).stdout
    exported_public = subprocess.run(
        [
            "/usr/bin/gpg",
            "--batch",
            "--export",
            fingerprint,
        ],
        env=gpg_environment,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    ).stdout

    package_root = tmp_path / "package"
    control_root = package_root / "DEBIAN"
    payload_root = package_root / "opt" / "chummer6"
    control_root.mkdir(parents=True)
    control_root.chmod(0o755)
    payload_root.mkdir(parents=True)
    fixture_deb_version = MODULE.normalize_debian_version(RELEASE_VERSION)
    (control_root / "control").write_text(
        "\n".join(
            [
                "Package: chummer6-avalonia",
                f"Version: {fixture_deb_version}",
                "Architecture: amd64",
                "Maintainer: Chummer test <fixture@example.invalid>",
                "Description: ephemeral signing fixture",
                "",
            ]
        ),
        encoding="utf-8",
    )
    (payload_root / "fixture.txt").write_text(
        "authenticated fixture payload\n", encoding="utf-8"
    )
    unsigned_root = tmp_path / "unsigned"
    unsigned_package = unsigned_root / "files" / MODULE.ARTIFACT_FILE_NAME
    unsigned_package.parent.mkdir(parents=True)
    subprocess.run(
        [
            "/usr/bin/dpkg-deb",
            "--build",
            "--root-owner-group",
            "-Zxz",
            str(package_root),
            str(unsigned_package),
        ],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    unsigned_snapshot = MODULE.snapshot(
        unsigned_package, "unsigned fixture", MODULE.MAX_PACKAGE_BYTES
    )
    generated_at = (
        datetime.now(UTC)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )
    unsigned_receipt = unsigned_root / "LINUX_NATIVE_UNSIGNED_EXPORT.json"
    source = {
        "actor": "fixture-operator",
        "ref": MODULE.REF,
        "repository": MODULE.REPOSITORY,
        "runAttempt": "1",
        "runId": "987654",
        "sha": SOURCE_SHA,
        "workflow": MODULE.WORKFLOW,
    }
    unsigned_receipt.write_text(
        json.dumps(
            {
                "artifact": {
                    "fileName": MODULE.ARTIFACT_FILE_NAME,
                    "memberPath": f"files/{MODULE.ARTIFACT_FILE_NAME}",
                    "sha256": unsigned_snapshot.sha256,
                    "sizeBytes": unsigned_snapshot.size_bytes,
                },
                "contractName": MODULE.EXPORT_CONTRACT,
                "contractVersion": 2,
                "generatedAt": generated_at,
                "livePredecessorAuthority": {
                    "liveReleaseChannelSha256": "2" * 64,
                    "nMinusOneReleaseSha256": "3" * 64,
                    "selectedTupleSha256": "4" * 64,
                },
                "nonPublishing": True,
                "package": {
                    "architecture": "amd64",
                    "name": "chummer6-avalonia",
                    "version": fixture_deb_version,
                },
                "releaseVersion": RELEASE_VERSION,
                "source": source,
                "status": "exported",
            },
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )

    signed_root = tmp_path / "signed"
    signed_package = signed_root / "files" / MODULE.ARTIFACT_FILE_NAME
    signing_receipt = (
        signed_root / "signing" / MODULE.SIGNING_RECEIPT_FILE_NAME
    )
    policy = (
        signed_root
        / "signing"
        / "policies"
        / fingerprint[-16:]
        / MODULE.POLICY_FILE_NAME
    )
    keyring = (
        signed_root
        / "signing"
        / "keyrings"
        / fingerprint[-16:]
        / MODULE.KEYRING_FILE_NAME
    )
    signed_export = (
        signed_root / MODULE.SIGNED_EXPORT_RECEIPT_FILE_NAME
    )
    transaction_manifest = (
        signed_root / MODULE.TRANSACTION_MANIFEST_FILE_NAME
    )
    monkeypatch.setenv(
        MODULE.PRIVATE_KEY_ENV,
        base64.b64encode(exported_secret).decode("ascii"),
    )
    monkeypatch.setenv(
        MODULE.PASSPHRASE_ENV,
        base64.b64encode(passphrase).decode("ascii"),
    )
    monkeypatch.setenv("RUNNER_TEMP", str(tmp_path))
    sign_result = MODULE._sign(
        SimpleNamespace(
            input_package=unsigned_package,
            output_package=signed_package,
            unsigned_export_receipt=unsigned_receipt,
            signed_export_receipt=signed_export,
            transaction_manifest=transaction_manifest,
            receipt=signing_receipt,
            policy=policy,
            public_keyring=keyring,
            release_version=RELEASE_VERSION,
            expected_fingerprint=fingerprint,
            expected_public_keyring_sha256=hashlib.sha256(
                exported_public
            ).hexdigest(),
            expected_unsigned_package_sha256=unsigned_snapshot.sha256,
            expected_unsigned_package_size=str(
                unsigned_snapshot.size_bytes
            ),
            expected_unsigned_export_receipt_sha256=sha256(
                unsigned_receipt
            ),
            artifact_member_path=f"files/{MODULE.ARTIFACT_FILE_NAME}",
            signing_receipt_member_path=(
                f"signing/{MODULE.SIGNING_RECEIPT_FILE_NAME}"
            ),
            policy_member_path=(
                f"signing/policies/{fingerprint[-16:]}/"
                f"{MODULE.POLICY_FILE_NAME}"
            ),
            public_keyring_member_path=(
                f"signing/keyrings/{fingerprint[-16:]}/"
                f"{MODULE.KEYRING_FILE_NAME}"
            ),
            source_repository=MODULE.REPOSITORY,
            source_workflow=MODULE.WORKFLOW,
            source_run_id="987654",
            source_run_attempt="1",
            source_ref=MODULE.REF,
            source_sha=SOURCE_SHA,
            source_actor="fixture-operator",
        )
    )
    published_root = tmp_path / "published-signed-output"
    stage_result = MODULE._stage_output_set(
        SimpleNamespace(
            package=signed_package,
            receipt=signing_receipt,
            signed_export_receipt=signed_export,
            policy=policy,
            public_keyring=keyring,
            transaction_manifest=transaction_manifest,
            output_root=published_root,
            expected_primary_fingerprint=fingerprint,
            expected_transaction_manifest_sha256=sign_result[
                "transactionManifestSha256"
            ],
        )
    )
    published_members = MODULE._canonical_transaction_members(fingerprint)
    verify_result = MODULE._verify(
        SimpleNamespace(
            package=published_root.joinpath(
                *Path(published_members["package"]).parts
            ),
            receipt=published_root.joinpath(
                *Path(published_members["signingReceipt"]).parts
            ),
            signed_export_receipt=published_root.joinpath(
                *Path(published_members["signedExportReceipt"]).parts
            ),
            transaction_manifest=(
                published_root / MODULE.TRANSACTION_MANIFEST_FILE_NAME
            ),
            policy=published_root.joinpath(
                *Path(published_members["policy"]).parts
            ),
            public_keyring=published_root.joinpath(
                *Path(published_members["publicKeyring"]).parts
            ),
            release_version=RELEASE_VERSION,
            expected_primary_fingerprint=fingerprint,
            expected_public_keyring_sha256=sign_result[
                "publicKeyringSha256"
            ],
            expected_signed_export_receipt_sha256=sign_result[
                "signedExportReceiptSha256"
            ],
            expected_transaction_manifest_sha256=sign_result[
                "transactionManifestSha256"
            ],
        )
    )

    assert verify_result["artifactSha256"] == sign_result["artifactSha256"]
    assert stage_result["transactionManifestSha256"] == sign_result[
        "transactionManifestSha256"
    ]
    assert verify_result["primaryFingerprint"] == fingerprint
    assert verify_result["tamperExitCode"] == 13
    assert not any(
        path.name in {"passphrase", "private-key"}
        for path in tmp_path.rglob("*")
    )
    assert not list(tmp_path.glob("chummer-linux-signing-*"))
    record_property("ephemeralFingerprint", fingerprint)
    record_property(
        "unsignedArtifactSha256", unsigned_snapshot.sha256
    )
    record_property(
        "unsignedArtifactSizeBytes", unsigned_snapshot.size_bytes
    )
    for key in (
        "artifactSha256",
        "artifactSizeBytes",
        "policySha256",
        "publicKeyringSha256",
        "signedExportReceiptSha256",
        "signingReceiptSha256",
        "transactionManifestSha256",
    ):
        record_property(key, sign_result[key])
    cleanup_generator_home()
    assert not generator_home.exists()
