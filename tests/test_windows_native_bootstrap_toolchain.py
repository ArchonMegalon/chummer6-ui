from __future__ import annotations

import copy
import json
import subprocess
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
BUILD_SCRIPT = REPO_ROOT / "scripts" / "build-native-windows-bootstrap-installer.sh"
LOCK_PATH = REPO_ROOT / "config" / "windows-native-bootstrap-toolchain.lock.json"

EXPECTED_IMAGE_INDEX_DIGEST = "sha256:7b140f374b289a7c2befc338f42ebe6441b7ea838a042bbd5acbfca6ec875818"
EXPECTED_AMD64_MANIFEST_DIGEST = "sha256:63a496b5d3b99214b39f5ed70eb71a61e590a77979c79cbee4faf991f8c0783e"
EXPECTED_PACKAGE_IDENTITIES = {
    "gcc-12-base": (
        "12.2.0-14+deb12u1",
        37596,
        "1896a2aacf4ad681ff5eacc24a5b0ca4d5d9c9b9c9e4b6de5197bc1e116ea619",
    ),
    "libc6": (
        "2.36-9+deb12u14",
        2759320,
        "ba4f88f73dbc3ae9055f3c20f4523bfdbaf1ad13ff95e258924f77d20b4fbedf",
    ),
    "libgcc-s1": (
        "12.2.0-14+deb12u1",
        49856,
        "3016e62cb4b7cd8038822870601f5ed131befe942774d0f745622cc77d8a88f7",
    ),
    "libstdc++6": (
        "12.2.0-14+deb12u1",
        612604,
        "5cd3171216d4ab0fc911cfe9c35509bf2dd8f47761c43b7f6a4296701551a24d",
    ),
    "nsis": (
        "3.08-3+deb12u1",
        552828,
        "b9ca8de84341753dd1c071a3f65453dc06bd7b6f2230140a36755d7e402827c2",
    ),
    "nsis-common": (
        "3.08-3+deb12u1",
        1154484,
        "f1c9e63389c947442fddd5ca446e22966e8f03fc10663f998cf7df58642f9b52",
    ),
    "p7zip": (
        "16.02+really26.01+dfsg-0+deb12u1",
        452948,
        "58b6aaf4fd163a4f5f7251792b870856dd63c249e62d505a20fcd0b6c2aefbc0",
    ),
    "p7zip-full": (
        "16.02+really26.01+dfsg-0+deb12u1",
        1423220,
        "91624c886bf525705beb66dd2bb063015c5cab4e51d9f08d9464cbbb53b00cb7",
    ),
    "zlib1g": (
        "1:1.2.13.dfsg-1",
        86684,
        "d7dd1d1411fedf27f5e27650a6eff20ef294077b568f4c8c5e51466dc7c08ce4",
    ),
}


def _lock() -> dict[str, object]:
    return json.loads(LOCK_PATH.read_text(encoding="utf-8"))


def _validate(lock_path: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [str(BUILD_SCRIPT), "--validate-toolchain-lock-only", str(lock_path)],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )


def _write_mutated_lock(tmp_path: Path, mutate) -> Path:
    payload = copy.deepcopy(_lock())
    mutate(payload)
    path = tmp_path / "toolchain.lock.json"
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return path


def test_lock_binds_exact_snapshot_image_and_complete_package_closure() -> None:
    payload = _lock()
    assert payload["contract_name"] == "chummer6-ui.windows_native_bootstrap_toolchain_lock"
    assert payload["schema_version"] == 1
    assert payload["platform"] == {"os": "linux", "architecture": "amd64"}
    assert payload["container_image"] == {
        "reference": f"docker.io/library/debian@{EXPECTED_IMAGE_INDEX_DIGEST}",
        "index_digest": EXPECTED_IMAGE_INDEX_DIGEST,
        "platform_manifest_digest": EXPECTED_AMD64_MANIFEST_DIGEST,
    }
    assert payload["debian_snapshot"] == {
        "timestamp": "20260713T000000Z",
        "archive_base_url": "https://snapshot.debian.org/archive/debian/20260713T000000Z",
        "suite": "bookworm",
        "component": "main",
        "metadata_url": (
            "https://snapshot.debian.org/archive/debian/20260713T000000Z/"
            "dists/bookworm/main/binary-amd64/Packages.xz"
        ),
        "install_roots": ["nsis", "p7zip-full"],
        "include_recommends": False,
    }

    packages = payload["packages"]
    assert isinstance(packages, list)
    assert [package["name"] for package in packages] == sorted(EXPECTED_PACKAGE_IDENTITIES)
    assert {
        package["name"]: (package["version"], package["size"], package["sha256"])
        for package in packages
    } == EXPECTED_PACKAGE_IDENTITIES
    assert all(
        package["url"].startswith(
            "https://snapshot.debian.org/archive/debian/20260713T000000Z/pool/main/"
        )
        for package in packages
    )
    assert all(package["url"].endswith(".deb") for package in packages)


def test_checked_in_lock_passes_strict_validator_and_shell_syntax() -> None:
    validation = _validate(LOCK_PATH)
    assert validation.returncode == 0, validation.stderr
    assert "toolchain lock is valid" in validation.stdout
    subprocess.run(["bash", "-n", str(BUILD_SCRIPT)], cwd=REPO_ROOT, check=True)


@pytest.mark.parametrize(
    "mutate",
    [
        lambda payload: payload.update({"unexpected": True}),
        lambda payload: payload["platform"].update({"architecture": "arm64"}),
        lambda payload: payload["container_image"].update(
            {"reference": "docker.io/library/debian:bookworm-slim"}
        ),
        lambda payload: payload["debian_snapshot"].update(
            {"archive_base_url": "https://deb.debian.org/debian"}
        ),
        lambda payload: payload["packages"][0].update({"size": "37596"}),
        lambda payload: payload["packages"][0].update({"sha256": "A" * 64}),
        lambda payload: payload["packages"][0].update(
            {"url": "https://example.invalid/gcc-12-base.deb"}
        ),
        lambda payload: payload["packages"].pop(),
        lambda payload: payload["packages"][4].update({"dependencies": ["libc6"]}),
    ],
)
def test_validator_rejects_non_immutable_or_incomplete_lock(tmp_path: Path, mutate) -> None:
    path = _write_mutated_lock(tmp_path, mutate)
    validation = _validate(path)
    assert validation.returncode != 0
    assert "Invalid Windows native bootstrap toolchain lock" in validation.stderr


def test_validator_rejects_duplicate_json_keys(tmp_path: Path) -> None:
    original = LOCK_PATH.read_text(encoding="utf-8")
    duplicate = original.replace(
        '  "schema_version": 1,',
        '  "schema_version": 1,\n  "schema_version": 1,',
        1,
    )
    path = tmp_path / "duplicate.lock.json"
    path.write_text(duplicate, encoding="utf-8")
    validation = _validate(path)
    assert validation.returncode != 0
    assert "duplicate JSON key 'schema_version'" in validation.stderr


def test_builder_prefetches_then_runs_exact_image_fully_offline() -> None:
    script = BUILD_SCRIPT.read_text(encoding="utf-8")
    assert "config/windows-native-bootstrap-toolchain.lock.json" in script
    assert "docker manifest inspect" in script
    assert "actual_digest != expected_digest" in script
    assert "docker pull --quiet --platform" in script
    assert "docker image inspect" in script
    assert "docker run --rm --pull never --network none --platform" in script
    assert '"$TOOLCHAIN_IMAGE"' in script
    assert "sha256sum --check --strict SHA256SUMS" in script
    assert "dpkg-deb --field" in script
    assert "dpkg --unpack /toolchain/debs/*.deb" in script
    assert "apt-get" not in script
    assert "debian:bookworm-slim" not in script
    assert "curl -L" not in script


def test_toolchain_cache_is_not_reachable_through_writable_work_mount() -> None:
    script = BUILD_SCRIPT.read_text(encoding="utf-8")
    assert "${STAGE_DIR}.windows-native-bootstrap-toolchain" in script
    assert '"$STAGE_DIR/.windows-native-bootstrap-toolchain"' not in script
    assert '"$TOOLCHAIN_CACHE_DIR" == "$STAGE_DIR/"*' in script
    assert '-v "$STAGE_DIR:/work"' in script
    assert '-v "$TOOLCHAIN_CACHE_DIR:/toolchain:ro"' in script


def test_existing_windows_asset_pins_are_preserved_and_verified_offline() -> None:
    script = BUILD_SCRIPT.read_text(encoding="utf-8")
    assert "https://github.com/ip7z/7zip/releases/download/26.02/7z2602-extra.7z" in script
    assert "081df9e9311dfd9c9e0e98c1c80180b99bb51e4cb24156b5f3057fe3c259d70a" in script
    assert "https://curl.se/windows/dl-8.21.0_1/curl-8.21.0_1-win64-mingw.zip" in script
    assert "157068447d5b0b178dcc650f29d4746049fa4c7cc12db5f2bc050c0b84e48e7a" in script
    assert "/toolchain/assets/7zip-extra.7z" in script
    assert "/toolchain/assets/curl-win64.zip" in script
    assert "Pinned Windows asset redirected away from HTTPS" in script
