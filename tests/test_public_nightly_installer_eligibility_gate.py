from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "verify-public-nightly-installer-eligibility.py"
PLATFORM_POLICY = REPO_ROOT / ".codex-design" / "product" / "DESKTOP_PLATFORM_ACCEPTANCE_MATRIX.yaml"
NIGHTLY_PUBLISHER = REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh"


def run_gate(stage: Path, *, policy: Path = PLATFORM_POLICY) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            sys.executable,
            str(SCRIPT),
            "--manifest",
            str(stage / "RELEASE_CHANNEL.generated.json"),
            "--files-dir",
            str(stage / "files"),
            "--platform-policy",
            str(policy),
        ],
        text=True,
        capture_output=True,
        check=False,
    )


def write_stage(stage: Path, row: dict[str, object], *, write_bytes: bool = True) -> None:
    files_dir = stage / "files"
    files_dir.mkdir(parents=True)
    (stage / "RELEASE_CHANNEL.generated.json").write_text(
        json.dumps({"version": "nightly-test", "channel": "preview", "artifacts": [row]}) + "\n",
        encoding="utf-8",
    )
    file_name = str(row.get("fileName") or "")
    if write_bytes and file_name:
        (files_dir / file_name).write_bytes(b"installer")


def write_shell_stage(stage: Path, row: dict[str, object]) -> Path:
    write_stage(stage, row)
    (stage / "releases.json").write_text(
        json.dumps({"version": "nightly-test", "channel": "preview", "downloads": [row]}) + "\n",
        encoding="utf-8",
    )
    handoff_materializer = stage.parent / "fake-handoff.py"
    handoff_materializer.write_text(
        "import json, pathlib, sys\n"
        "stage = pathlib.Path(sys.argv[1])\n"
        "(stage / 'RELEASE_BUILD_HANDOFF.generated.json').write_text(\n"
        "    json.dumps({'channel': 'preview', 'stage_proof_complete': False}) + '\\n',\n"
        "    encoding='utf-8',\n"
        ")\n",
        encoding="utf-8",
    )
    return handoff_materializer


def run_nightly_publisher(
    stage: Path,
    deploy_dir: Path,
    handoff_materializer: Path,
    *,
    support_only: bool,
) -> subprocess.CompletedProcess[str]:
    env = {
        "PATH": str(Path(sys.executable).parent) + ":/usr/bin:/bin",
        "CHUMMER_STAGING_ROOT": str(stage),
        "CHUMMER_REDEPLOY_PUBLIC_EDGE_AFTER_NIGHTLY_PUBLISH": "false",
        "CHUMMER_RELEASE_BUILD_HANDOFF_SCRIPT_PATH": str(handoff_materializer),
        "CHUMMER_FORCE_NIGHTLY_PUBLISH": "1",
        "CHUMMER_NIGHTLY_SUPPORT_PROOF_ONLY_HANDOFF": "1" if support_only else "0",
    }
    return subprocess.run(
        ["bash", str(NIGHTLY_PUBLISHER), str(deploy_dir)],
        text=True,
        capture_output=True,
        check=False,
        env=env,
    )


@pytest.mark.parametrize(
    ("row", "expected_platform"),
    [
        (
            {
                "artifactId": "avalonia-win-x64-installer",
                "platform": "windows",
                "kind": "installer",
                "fileName": "chummer-avalonia-win-x64-installer.exe",
                "installAccessClass": "open_public",
                "compatibilityState": "compatible",
            },
            "windows",
        ),
        (
            {
                "artifactId": "avalonia-linux-x64-installer",
                "platformId": "linux",
                "kind": "installer",
                "format": "deb",
                "fileName": "chummer-avalonia-linux-x64-installer.deb",
                "installAccessClass": "open_public",
            },
            "linux",
        ),
    ],
)
def test_gate_accepts_open_public_installer_for_promoted_platform(
    tmp_path: Path,
    row: dict[str, object],
    expected_platform: str,
) -> None:
    write_stage(tmp_path, row)

    result = run_gate(tmp_path)

    assert result.returncode == 0, result.stderr
    assert "public_nightly_installer_eligibility:ok" in result.stdout
    assert f"platform={expected_platform}" in result.stdout


@pytest.mark.parametrize(
    ("row", "reason"),
    [
        (
            {
                "artifactId": "avalonia-osx-arm64-installer",
                "platform": "macos",
                "kind": "installer",
                "fileName": "chummer-avalonia-osx-arm64-installer.dmg",
                "installAccessClass": "open_public",
            },
            "is not a promoted Windows/Linux release platform",
        ),
        (
            {
                "artifactId": "avalonia-win-x64-installer",
                "platform": "windows",
                "kind": "installer",
                "fileName": "chummer-avalonia-win-x64-installer.exe",
                "installAccessClass": "account_required",
            },
            "installAccessClass is not open_public",
        ),
        (
            {
                "artifactId": "avalonia-linux-x64-installer",
                "platform": "linux",
                "kind": "installer",
                "fileName": "chummer-avalonia-linux-x64-installer.deb",
                "installAccessClass": "open_public",
                "promotionState": "support_only",
            },
            "promotionState is support_only",
        ),
    ],
)
def test_gate_rejects_hidden_or_support_only_artifact_sets(
    tmp_path: Path,
    row: dict[str, object],
    reason: str,
) -> None:
    write_stage(tmp_path, row)

    result = run_gate(tmp_path)

    assert result.returncode == 1
    assert "public_nightly_installer_eligibility:fail" in result.stderr
    assert reason in result.stderr


def test_gate_rejects_manifest_only_installer_without_staged_bytes(tmp_path: Path) -> None:
    write_stage(
        tmp_path,
        {
            "artifactId": "avalonia-win-x64-installer",
            "platform": "windows",
            "kind": "installer",
            "fileName": "chummer-avalonia-win-x64-installer.exe",
            "installAccessClass": "open_public",
        },
        write_bytes=False,
    )

    result = run_gate(tmp_path)

    assert result.returncode == 1
    assert "staged artifact bytes are missing" in result.stderr


def test_gate_obeys_shared_platform_promotion_status(tmp_path: Path) -> None:
    row = {
        "artifactId": "avalonia-win-x64-installer",
        "platform": "windows",
        "kind": "installer",
        "fileName": "chummer-avalonia-win-x64-installer.exe",
        "installAccessClass": "open_public",
    }
    write_stage(tmp_path / "stage", row)
    policy = tmp_path / "DESKTOP_PLATFORM_ACCEPTANCE_MATRIX.yaml"
    policy.write_text(
        "platforms:\n"
        "  - id: windows\n"
        "    public_shelf_status: buildable_not_publicly_promoted\n"
        "    primary_package_kind: installer\n"
        "  - id: macOS\n"
        "    public_shelf_status: buildable_not_publicly_promoted\n"
        "    primary_package_kind: none\n",
        encoding="utf-8",
    )

    result = run_gate(tmp_path / "stage", policy=policy)

    assert result.returncode != 0
    assert "no promoted Windows/Linux release platform" in result.stderr


def test_generic_nightly_rejects_mac_only_stage_before_shelf_mutation(tmp_path: Path) -> None:
    stage = tmp_path / "stage"
    deploy_dir = tmp_path / "downloads"
    deploy_dir.mkdir()
    sentinel = deploy_dir / "existing-shelf.txt"
    sentinel.write_text("keep", encoding="utf-8")
    handoff_materializer = write_shell_stage(
        stage,
        {
            "artifactId": "avalonia-osx-arm64-installer",
            "platform": "macos",
            "kind": "installer",
            "fileName": "chummer-avalonia-osx-arm64-installer.dmg",
            "installAccessClass": "account_required",
        },
    )

    result = run_nightly_publisher(
        stage,
        deploy_dir,
        handoff_materializer,
        support_only=False,
    )

    assert result.returncode == 1
    assert "public_nightly_installer_eligibility:fail" in result.stderr
    assert "Public nightly requires at least one staged open-public Windows/Linux installer" in result.stderr
    assert sentinel.read_text(encoding="utf-8") == "keep"
    assert not (deploy_dir / "RELEASE_CHANNEL.generated.json").exists()
    assert "Publishing latest nightly stage:" not in result.stdout


def test_support_proof_only_handoff_accepts_mac_stage_without_shelf_mutation(tmp_path: Path) -> None:
    stage = tmp_path / "stage"
    deploy_dir = tmp_path / "downloads"
    deploy_dir.mkdir()
    sentinel = deploy_dir / "existing-shelf.txt"
    sentinel.write_text("keep", encoding="utf-8")
    handoff_materializer = write_shell_stage(
        stage,
        {
            "artifactId": "avalonia-osx-arm64-installer",
            "platform": "macos",
            "kind": "installer",
            "fileName": "chummer-avalonia-osx-arm64-installer.dmg",
            "installAccessClass": "account_required",
        },
    )

    result = run_nightly_publisher(
        stage,
        deploy_dir,
        handoff_materializer,
        support_only=True,
    )

    assert result.returncode == 0, result.stderr
    assert "Prepared support/proof-only nightly handoff:" in result.stdout
    assert "Public downloads shelf unchanged; no public nightly was published." in result.stdout
    assert sentinel.read_text(encoding="utf-8") == "keep"
    assert not (deploy_dir / "RELEASE_CHANNEL.generated.json").exists()
    assert "Publishing latest nightly stage:" not in result.stdout
