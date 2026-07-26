from __future__ import annotations

import copy
import base64
import hashlib
import importlib.util
import json
import os
import subprocess
import sys
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
        ar_member("control.tar.xz", b"control-fixture"),
        ar_member("data.tar.xz", b"authenticated-data-member-fixture"),
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
    decoded: list[str] = []
    monkeypatch.setattr(
        MODULE,
        "_decode_secret_environment",
        lambda name, *_: decoded.append(name) or b"secret",
    )
    args = SimpleNamespace(
        input_package=unsigned,
        output_package=tmp_path / "signed" / MODULE.ARTIFACT_FILE_NAME,
        unsigned_export_receipt=tmp_path / "unsigned-export.json",
        signed_export_receipt=tmp_path / "signed-export.json",
        receipt=tmp_path / MODULE.SIGNING_RECEIPT_FILE_NAME,
        policy=tmp_path / MODULE.POLICY_FILE_NAME,
        public_keyring=tmp_path / MODULE.KEYRING_FILE_NAME,
        release_version=RELEASE_VERSION,
        expected_fingerprint=FINGERPRINT,
        expected_public_keyring_sha256="a" * 64,
        expected_unsigned_package_sha256=held.sha256,
        expected_unsigned_package_size=str(held.size_bytes),
        expected_unsigned_export_receipt_sha256="b" * 64,
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
    args = SimpleNamespace(
        package=package.path,
        policy=policy.path,
        public_keyring=keyring.path,
        receipt=receipt_path,
        release_version=RELEASE_VERSION,
        expected_primary_fingerprint=FINGERPRINT,
        expected_public_keyring_sha256="f" * 64,
    )

    with pytest.raises(MODULE.ContractError, match="independent lifecycle"):
        MODULE._verify(args)


def test_secret_environment_is_not_forwarded_to_child_tools(
    tmp_path: Path,
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


@pytest.mark.skipif(
    not REAL_TOOLS_AVAILABLE,
    reason="exact Debian origin-signing tools are not installed",
)
def test_real_debsigs_029_round_trip_uses_only_ephemeral_fixture_key(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
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
    verify_result = MODULE._verify(
        SimpleNamespace(
            package=signed_package,
            receipt=signing_receipt,
            signed_export_receipt=signed_export,
            policy=policy,
            public_keyring=keyring,
            release_version=RELEASE_VERSION,
            expected_primary_fingerprint=fingerprint,
            expected_public_keyring_sha256=sign_result[
                "publicKeyringSha256"
            ],
            expected_signed_export_receipt_sha256=sign_result[
                "signedExportReceiptSha256"
            ],
        )
    )

    assert verify_result["artifactSha256"] == sign_result["artifactSha256"]
    assert verify_result["primaryFingerprint"] == fingerprint
    assert verify_result["tamperExitCode"] == 13
    assert not any(
        path.name in {"passphrase", "private-key"}
        for path in tmp_path.rglob("*")
    )
    subprocess.run(
        ["/usr/bin/gpgconf", "--homedir", str(generator_home), "--kill", "all"],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
