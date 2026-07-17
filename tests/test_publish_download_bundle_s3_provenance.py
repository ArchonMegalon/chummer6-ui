from __future__ import annotations

import hashlib
import json
import os
import subprocess
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHER = REPO_ROOT / "scripts" / "publish-download-bundle-s3.sh"
RUNBOOK = REPO_ROOT / "scripts" / "runbook.sh"


def write_executable(path: Path, body: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(body, encoding="utf-8")
    path.chmod(0o755)


def tree_bytes(root: Path) -> dict[str, tuple[str, bytes | str | None]]:
    snapshot: dict[str, tuple[str, bytes | str | None]] = {}
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root).as_posix()
        if path.is_symlink():
            snapshot[relative] = ("symlink", os.readlink(path))
        elif path.is_dir():
            snapshot[relative] = ("directory", None)
        elif path.is_file():
            snapshot[relative] = ("file", path.read_bytes())
        else:
            snapshot[relative] = ("special", None)
    return snapshot


def write_bundle(bundle: Path, *, platform: str) -> None:
    artifact_name = (
        "chummer-avalonia-osx-arm64-installer.dmg"
        if platform == "macos"
        else "chummer-avalonia-linux-x64-installer.deb"
    )
    artifact = bundle / "files" / artifact_name
    artifact.parent.mkdir(parents=True)
    artifact.write_bytes(b"candidate-bytes")
    digest = hashlib.sha256(artifact.read_bytes()).hexdigest()
    row = {
        "artifactId": artifact_name.removeprefix("chummer-").removesuffix(".dmg").removesuffix(".deb"),
        "platform": platform,
        "fileName": artifact_name,
        "downloadUrl": f"/downloads/files/{artifact_name}",
        "sha256": digest,
        "sizeBytes": artifact.stat().st_size,
    }
    canonical = {
        "version": "candidate-v2",
        "channel": "preview",
        "supportabilityState": "review_required",
        "artifacts": [row],
    }
    compatibility = {
        "version": "candidate-v2",
        "channel": "preview",
        "downloads": [{"id": row["artifactId"], "url": row["downloadUrl"], "sha256": digest}],
    }
    (bundle / "RELEASE_CHANNEL.generated.json").write_text(
        json.dumps(canonical, indent=2) + "\n", encoding="utf-8"
    )
    (bundle / "releases.json").write_text(
        json.dumps(compatibility, indent=2) + "\n", encoding="utf-8"
    )
    if platform == "macos":
        proof = bundle / "proof" / "build-provenance" / "v1" / "invocations"
        proof.mkdir(parents=True)
        (proof / "receipt.json").write_bytes(b"candidate-proof")


def seed_valid_remote_shelf(root: Path, *, platform: str, same_size_stale: bool = False) -> None:
    artifact_name = (
        "chummer-avalonia-osx-arm64-installer.dmg"
        if platform == "macos"
        else "chummer-avalonia-linux-x64-installer.deb"
    )
    artifact = root / "files" / artifact_name
    artifact.parent.mkdir(parents=True)
    artifact_bytes = b"old-valid-bytes"
    if same_size_stale:
        artifact_bytes = b"stale-same-size"
    artifact.write_bytes(artifact_bytes)
    digest = hashlib.sha256(artifact_bytes).hexdigest()
    manifest = {
        "version": "old-valid-v1",
        "channel": "preview",
        "supportabilityState": "review_required",
        "artifacts": [
            {
                "artifactId": artifact_name,
                "platform": platform,
                "fileName": artifact_name,
                "downloadUrl": f"/downloads/files/{artifact_name}",
                "sha256": digest,
                "sizeBytes": len(artifact_bytes),
            }
        ],
    }
    encoded = json.dumps(manifest, indent=2) + "\n"
    (root / "RELEASE_CHANNEL.generated.json").write_text(encoded, encoding="utf-8")
    (root / "releases.json").write_text(encoded, encoding="utf-8")
    if platform == "macos":
        proof = root / "proof" / "build-provenance" / "v1" / "invocations"
        proof.mkdir(parents=True)
        (proof / "receipt.json").write_bytes(b"old-valid-proof")


def assert_remote_shelf_valid(root: Path) -> None:
    manifest = json.loads((root / "RELEASE_CHANNEL.generated.json").read_text(encoding="utf-8"))
    assert manifest["artifacts"]
    for row in manifest["artifacts"]:
        artifact = root / "files" / row["fileName"]
        assert artifact.is_file()
        assert artifact.stat().st_size == row["sizeBytes"]
        assert hashlib.sha256(artifact.read_bytes()).hexdigest() == row["sha256"]
        if row["platform"] == "macos":
            assert (root / "proof" / "build-provenance" / "v1" / "invocations" / "receipt.json").is_file()


def make_hostile_environment(tmp_path: Path, remote_root: Path) -> dict[str, str]:
    fake_bin = tmp_path / "fake-bin"
    aws_log = tmp_path / "aws-called.log"
    write_executable(
        fake_bin / "aws",
        """#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >>"${AWS_CALL_LOG:?}"
printf 'remote-corruption' >"${FAKE_REMOTE_ROOT:?}/files/corrupted-by-aws.bin"
exit "${FAKE_AWS_EXIT_CODE:-0}"
""",
    )
    fake_bash_marker = tmp_path / "fake-bash-executed.marker"
    write_executable(
        fake_bin / "bash",
        f"#!/bin/sh\nprintf 'executed\\n' >{str(fake_bash_marker)!r}\nexit 97\n",
    )

    bash_env_marker = tmp_path / "bash-env-executed.marker"
    bash_env = tmp_path / "hostile-bash-env.sh"
    bash_env.write_text(
        f"printf 'executed\\n' >{str(bash_env_marker)!r}\n"
        "aws s3 cp hostile s3://fixture/hostile\n",
        encoding="utf-8",
    )
    exported_function_marker = tmp_path / "exported-function-executed.marker"

    import_root = tmp_path / "hostile-pythonpath"
    import_root.mkdir()
    sitecustomize_marker = tmp_path / "sitecustomize-imported.marker"
    (import_root / "sitecustomize.py").write_text(
        "from pathlib import Path\n"
        f"Path({str(sitecustomize_marker)!r}).write_text('imported', encoding='utf-8')\n",
        encoding="utf-8",
    )
    validator_marker = tmp_path / "validator-executed.marker"
    validator = tmp_path / "hostile-validator.py"
    validator.write_text(
        "from pathlib import Path\n"
        f"Path({str(validator_marker)!r}).write_text('executed', encoding='utf-8')\n",
        encoding="utf-8",
    )

    env = os.environ.copy()
    env.update(
        {
            "PATH": f"{fake_bin}{os.pathsep}{env['PATH']}",
            "BASH_ENV": str(bash_env),
            "ENV": str(bash_env),
            "BASH_FUNC_printf%%": f"() {{ : > {str(exported_function_marker)!r}; }}",
            "BASH_FUNC_pwd%%": f"() {{ : > {str(exported_function_marker)!r}; builtin pwd \"$@\"; }}",
            "BASH_FUNC_cd%%": f"() {{ : > {str(exported_function_marker)!r}; builtin cd \"$@\"; }}",
            "BASH_FUNC_exec%%": f"() {{ : > {str(exported_function_marker)!r}; builtin exec \"$@\"; }}",
            "SHELLOPTS": "braceexpand:errexit:hashall:interactive-comments:nounset:xtrace",
            "CDPATH": str(tmp_path / "hostile-cdpath"),
            "PYTHONPATH": str(import_root),
            "AWS_CALL_LOG": str(aws_log),
            "FAKE_REMOTE_ROOT": str(remote_root),
            "CHUMMER_RELEASE_BUILD_PROVENANCE_VALIDATOR": str(validator),
            "CHUMMER_PORTAL_DOWNLOADS_S3_URI": "s3://fixture/downloads",
            "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": "https://example.invalid/downloads/RELEASE_CHANNEL.generated.json",
        }
    )
    return env


@pytest.mark.parametrize(
    ("candidate_platform", "remote_platform", "latest", "endpoint", "failure_phase"),
    [
        ("macos", "macos", False, False, "artifact"),
        ("macos", "macos", True, True, "proof"),
        ("linux", "macos", False, False, "non-mac-transition"),
        ("linux", "linux", True, False, "canonical"),
        ("macos", "linux", True, True, "second-target"),
    ],
)
def test_every_s3_mode_fails_before_process_or_remote_mutation(
    tmp_path: Path,
    candidate_platform: str,
    remote_platform: str,
    latest: bool,
    endpoint: bool,
    failure_phase: str,
) -> None:
    bundle = tmp_path / "bundle"
    write_bundle(bundle, platform=candidate_platform)
    remote = tmp_path / "fake-s3" / "downloads"
    seed_valid_remote_shelf(remote, platform=remote_platform, same_size_stale=True)
    latest_remote = tmp_path / "fake-s3" / "latest"
    seed_valid_remote_shelf(latest_remote, platform=remote_platform)
    mirror = tmp_path / "guarded-mirror"
    seed_valid_remote_shelf(mirror, platform=remote_platform)

    bundle_before = tree_bytes(bundle)
    remote_before = tree_bytes(remote)
    latest_before = tree_bytes(latest_remote)
    mirror_before = tree_bytes(mirror)
    env = make_hostile_environment(tmp_path, remote)
    env["FAKE_AWS_FAIL_PHASE"] = failure_phase
    env["CHUMMER_S3_MIRROR_GUARD_DIRS"] = str(mirror)
    if latest:
        env["CHUMMER_PORTAL_DOWNLOADS_S3_LATEST_URI"] = "s3://fixture/latest"
    if endpoint:
        env["CHUMMER_PORTAL_DOWNLOADS_S3_ENDPOINT_URL"] = "https://s3.invalid"

    result = subprocess.run(
        [str(PUBLISHER), str(bundle)],
        cwd=REPO_ROOT,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 78
    assert result.stdout == ""
    assert "Object-storage release publication is disabled fail-closed" in result.stderr
    assert "scripts/publish-download-bundle-http.sh" in result.stderr
    assert "scripts/publish-download-bundle.sh" in result.stderr
    assert "immutable, versioned artifact and proof object keys" in result.stderr
    assert "one atomic canonical pointer cutover" in result.stderr
    assert "No resolver, generator, validator, local mirror, or AWS command was invoked" in result.stderr
    assert not (tmp_path / "aws-called.log").exists()
    assert not (tmp_path / "sitecustomize-imported.marker").exists()
    assert not (tmp_path / "validator-executed.marker").exists()
    assert not (tmp_path / "bash-env-executed.marker").exists()
    assert not (tmp_path / "exported-function-executed.marker").exists()
    assert not (tmp_path / "fake-bash-executed.marker").exists()
    assert tree_bytes(bundle) == bundle_before
    assert tree_bytes(remote) == remote_before
    assert tree_bytes(latest_remote) == latest_before
    assert tree_bytes(mirror) == mirror_before
    assert_remote_shelf_valid(remote)
    assert_remote_shelf_valid(latest_remote)
    assert_remote_shelf_valid(mirror)


def test_s3_fail_closed_boundary_does_not_require_configuration_or_existing_bundle(
    tmp_path: Path,
) -> None:
    missing_bundle = tmp_path / "missing"
    result = subprocess.run(
        [str(PUBLISHER), str(missing_bundle)],
        cwd=REPO_ROOT,
        env={"PATH": os.environ["PATH"]},
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 78
    assert "disabled fail-closed" in result.stderr


def test_downloads_sync_s3_runbook_mode_propagates_ex_config_without_state_change(
    tmp_path: Path,
) -> None:
    bundle = tmp_path / "bundle"
    write_bundle(bundle, platform="macos")
    remote = tmp_path / "fake-s3" / "downloads"
    seed_valid_remote_shelf(remote, platform="macos", same_size_stale=True)
    mirror = tmp_path / "guarded-mirror"
    seed_valid_remote_shelf(mirror, platform="macos")
    bundle_before = tree_bytes(bundle)
    remote_before = tree_bytes(remote)
    mirror_before = tree_bytes(mirror)
    env = make_hostile_environment(tmp_path, remote)
    log_root = tmp_path / "forbidden-log-state"
    runbook_state_root = tmp_path / "forbidden-runbook-state"
    env.update(
        {
            "RUNBOOK_MODE": "downloads-sync-s3",
            "DOWNLOAD_BUNDLE_DIR": str(bundle),
            "CHUMMER_S3_MIRROR_GUARD_DIRS": str(mirror),
            "RUNBOOK_LOG_DIR": str(log_root),
            "RUNBOOK_STATE_DIR": str(runbook_state_root),
        }
    )

    result = subprocess.run(
        [str(RUNBOOK)],
        cwd=REPO_ROOT,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode == 78
    combined_output = result.stdout + result.stderr
    assert "Object-storage release publication is disabled fail-closed" in combined_output
    assert "scripts/publish-download-bundle-http.sh" in combined_output
    assert "scripts/publish-download-bundle.sh" in combined_output
    assert result.stdout == ""
    assert not (tmp_path / "aws-called.log").exists()
    assert not (tmp_path / "sitecustomize-imported.marker").exists()
    assert not (tmp_path / "validator-executed.marker").exists()
    assert not (tmp_path / "bash-env-executed.marker").exists()
    assert not (tmp_path / "exported-function-executed.marker").exists()
    assert not (tmp_path / "fake-bash-executed.marker").exists()
    assert not log_root.exists()
    assert not runbook_state_root.exists()
    assert tree_bytes(bundle) == bundle_before
    assert tree_bytes(remote) == remote_before
    assert tree_bytes(mirror) == mirror_before
    assert_remote_shelf_valid(remote)
    assert_remote_shelf_valid(mirror)


def test_s3_script_contains_no_reachable_publication_or_validation_process() -> None:
    source = PUBLISHER.read_text(encoding="utf-8")

    assert source.startswith("#!/bin/bash -p\n")
    assert RUNBOOK.read_text(encoding="utf-8").startswith("#!/bin/bash -p\n")
    assert "exit 78" in source
    assert "\naws s3 " not in source
    assert "\npython3 " not in source
    assert "generate-releases-manifest.sh" not in source
    assert "verify_release_build_provenance_bundle.py" not in source
