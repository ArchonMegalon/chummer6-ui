from __future__ import annotations

import hashlib
import json
import os
import subprocess
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]
TOOL = ROOT / "scripts" / "macos_flagship_candidate_escrow.mjs"
CANDIDATE_NAME = "chummer-avalonia-osx-arm64-installer.dmg"
RECEIPT_NAME = "MACOS_FLAGSHIP_CANDIDATE_ESCROW.generated.json"
CIPHERTEXT_NAME = CANDIDATE_NAME + ".aes256gcm"


def generate_keypair(root: Path, bits: int) -> tuple[Path, Path, str]:
    private_key = root / f"private-{bits}.pem"
    public_key = root / f"public-{bits}.pem"
    script = r"""
const { generateKeyPairSync, createHash } = require("node:crypto");
const { writeFileSync } = require("node:fs");
const bits = Number(process.env.TEST_RSA_BITS);
const pair = generateKeyPairSync("rsa", {
  modulusLength: bits,
  publicExponent: 0x10001,
});
writeFileSync(process.env.TEST_PRIVATE_KEY, pair.privateKey.export({
  format: "pem",
  type: "pkcs8",
}), { mode: 0o600 });
writeFileSync(process.env.TEST_PUBLIC_KEY, pair.publicKey.export({
  format: "pem",
  type: "spki",
}), { mode: 0o600 });
const der = pair.publicKey.export({ format: "der", type: "spki" });
process.stdout.write(createHash("sha256").update(der).digest("hex"));
"""
    environment = {
        **os.environ,
        "TEST_PRIVATE_KEY": str(private_key),
        "TEST_PUBLIC_KEY": str(public_key),
        "TEST_RSA_BITS": str(bits),
    }
    result = subprocess.run(
        ("node", "-e", script),
        cwd=ROOT,
        env=environment,
        check=False,
        capture_output=True,
        text=True,
    )
    assert result.returncode == 0, result.stderr
    assert len(result.stdout) == 64
    return private_key, public_key, result.stdout


@pytest.fixture(scope="module")
def rsa_authority(
    tmp_path_factory: pytest.TempPathFactory,
) -> tuple[Path, Path, str]:
    return generate_keypair(tmp_path_factory.mktemp("macos-escrow-rsa"), 3072)


def run_tool(*arguments: object) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ("node", str(TOOL), *(str(value) for value in arguments)),
        cwd=ROOT,
        check=False,
        capture_output=True,
        text=True,
    )


def seal_command(
    candidate: Path,
    output: Path,
    public_key: Path,
    pin: str,
) -> tuple[object, ...]:
    data = candidate.read_bytes()
    return (
        "seal",
        "--candidate",
        candidate,
        "--output-dir",
        output,
        "--recipient-public-key",
        public_key,
        "--recipient-spki-sha256",
        pin,
        "--expected-candidate-sha256",
        hashlib.sha256(data).hexdigest(),
        "--expected-candidate-size",
        len(data),
        "--candidate-id",
        "candidate-20260725",
        "--generation-id",
        "generation-20260725",
        "--release-version",
        "run-20260725-120000",
        "--repository",
        "ArchonMegalon/chummer6-ui",
        "--workflow",
        ".github/workflows/macos-flagship-evidence.yml",
        "--environment",
        "macos-flagship-evidence",
        "--ref",
        "refs/heads/main",
        "--sha",
        "1" * 40,
        "--actor",
        "release-operator",
        "--run-id",
        "100",
        "--run-attempt",
        "2",
    )


def test_seal_and_open_preserve_exact_candidate_without_plaintext_distribution(
    tmp_path: Path,
    rsa_authority: tuple[Path, Path, str],
) -> None:
    private_key, public_key, pin = rsa_authority
    candidate = tmp_path / CANDIDATE_NAME
    plaintext = b"signed-and-notarized-dmg\0" * 4096
    candidate.write_bytes(plaintext)
    escrow = tmp_path / "escrow"

    sealed = run_tool(*seal_command(candidate, escrow, public_key, pin))

    assert sealed.returncode == 0, sealed.stderr
    receipt_path = escrow / RECEIPT_NAME
    ciphertext_path = escrow / CIPHERTEXT_NAME
    receipt_raw = receipt_path.read_bytes()
    receipt = json.loads(receipt_raw)
    assert receipt_raw == json.dumps(
        receipt,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=False,
    ).encode()
    assert ciphertext_path.read_bytes() != plaintext
    assert ciphertext_path.stat().st_size == len(plaintext)
    assert receipt["candidate"]["sha256"] == hashlib.sha256(plaintext).hexdigest()
    assert receipt["ciphertext"]["sha256"] == hashlib.sha256(
        ciphertext_path.read_bytes()
    ).hexdigest()
    assert receipt["recipient"]["spkiSha256"] == pin
    assert receipt["encryption"]["cipher"] == "aes-256-gcm"
    assert receipt["encryption"]["keyWrap"] == "rsa-oaep-sha256"
    assert private_key.read_text() not in receipt_raw.decode()

    output = tmp_path / "opened" / CANDIDATE_NAME
    output.parent.mkdir(mode=0o700)
    opened = run_tool(
        "open",
        "--receipt",
        receipt_path,
        "--ciphertext",
        ciphertext_path,
        "--private-key",
        private_key,
        "--expected-recipient-spki-sha256",
        pin,
        "--output",
        output,
    )

    assert opened.returncode == 0, opened.stderr
    assert output.read_bytes() == plaintext


def test_open_rejects_ciphertext_tampering_and_removes_partial_plaintext(
    tmp_path: Path,
    rsa_authority: tuple[Path, Path, str],
) -> None:
    private_key, public_key, pin = rsa_authority
    candidate = tmp_path / CANDIDATE_NAME
    candidate.write_bytes(b"exact-candidate-bytes")
    escrow = tmp_path / "escrow"
    sealed = run_tool(*seal_command(candidate, escrow, public_key, pin))
    assert sealed.returncode == 0, sealed.stderr
    ciphertext = escrow / CIPHERTEXT_NAME
    tampered = bytearray(ciphertext.read_bytes())
    tampered[-1] ^= 1
    ciphertext.write_bytes(tampered)
    output = tmp_path / "opened" / CANDIDATE_NAME
    output.parent.mkdir(mode=0o700)

    result = run_tool(
        "open",
        "--receipt",
        escrow / RECEIPT_NAME,
        "--ciphertext",
        ciphertext,
        "--private-key",
        private_key,
        "--expected-recipient-spki-sha256",
        pin,
        "--output",
        output,
    )

    assert result.returncode != 0
    assert "ciphertext changed or did not match" in result.stderr
    assert not output.exists()
    assert list(output.parent.iterdir()) == []


def test_seal_rejects_weak_or_unpinned_recipient_authority(
    tmp_path: Path,
    rsa_authority: tuple[Path, Path, str],
) -> None:
    _, strong_public_key, strong_pin = rsa_authority
    candidate = tmp_path / CANDIDATE_NAME
    candidate.write_bytes(b"candidate")

    wrong_pin = run_tool(
        *seal_command(
            candidate,
            tmp_path / "wrong-pin",
            strong_public_key,
            "f" * 64,
        )
    )
    assert wrong_pin.returncode != 0
    assert "SPKI SHA-256" in wrong_pin.stderr
    assert not (tmp_path / "wrong-pin").exists()

    _, weak_public_key, weak_pin = generate_keypair(tmp_path, 2048)
    weak = run_tool(
        *seal_command(
            candidate,
            tmp_path / "weak-key",
            weak_public_key,
            weak_pin,
        )
    )
    assert weak.returncode != 0
    assert "RSA 3072-8192" in weak.stderr
    assert not (tmp_path / "weak-key").exists()


def test_symlink_candidate_and_noncanonical_receipt_fail_closed(
    tmp_path: Path,
    rsa_authority: tuple[Path, Path, str],
) -> None:
    private_key, public_key, pin = rsa_authority
    real_candidate = tmp_path / "real.dmg"
    real_candidate.write_bytes(b"candidate")
    linked_candidate = tmp_path / CANDIDATE_NAME
    linked_candidate.symlink_to(real_candidate.name)
    linked = run_tool(
        *seal_command(
            linked_candidate,
            tmp_path / "linked",
            public_key,
            pin,
        )
    )
    assert linked.returncode != 0
    assert "non-symlink" in linked.stderr

    linked_candidate.unlink()
    linked_candidate.write_bytes(b"candidate")
    escrow = tmp_path / "escrow"
    sealed = run_tool(*seal_command(linked_candidate, escrow, public_key, pin))
    assert sealed.returncode == 0, sealed.stderr
    receipt_path = escrow / RECEIPT_NAME
    payload = json.loads(receipt_path.read_text())
    payload["unexpected"] = True
    receipt_path.write_text(
        json.dumps(payload, sort_keys=True, separators=(",", ":")),
        encoding="utf-8",
    )
    output = tmp_path / "opened" / CANDIDATE_NAME
    output.parent.mkdir(mode=0o700)

    result = run_tool(
        "open",
        "--receipt",
        receipt_path,
        "--ciphertext",
        escrow / CIPHERTEXT_NAME,
        "--private-key",
        private_key,
        "--expected-recipient-spki-sha256",
        pin,
        "--output",
        output,
    )

    assert result.returncode != 0
    assert "missing or extra fields" in result.stderr
    assert not output.exists()
