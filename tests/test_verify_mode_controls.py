from __future__ import annotations

import json
import os
import hashlib
import subprocess
import sys
from datetime import UTC, datetime, timedelta
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
CONTRACT = REPO_ROOT / "scripts" / "ai" / "verify_mode_contract.py"
VERIFY = REPO_ROOT / "scripts" / "ai" / "verify.sh"
PACKAGE_PLANE = REPO_ROOT / "scripts" / "ai" / "with-package-plane.sh"


def run_contract(*args: object) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(CONTRACT), *(str(value) for value in args)],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
        check=False,
    )


def strict_feed_environment(tmp_path: Path) -> dict[str, str]:
    feed = tmp_path / "feed"
    feed.mkdir()
    package = feed / "Pinned.1.0.0.nupkg"
    package.write_bytes(b"pinned-package")
    config = tmp_path / "NuGet.Config"
    config.write_text(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        "<configuration>\n"
        "  <packageSources>\n"
        "    <clear />\n"
        f"    <add key=\"same-run-local-feed\" value=\"{feed.as_posix()}\" />\n"
        "  </packageSources>\n"
        "  <packageSourceMapping>\n"
        "    <packageSource key=\"same-run-local-feed\">\n"
        "      <package pattern=\"*\" />\n"
        "    </packageSource>\n"
        "  </packageSourceMapping>\n"
        "</configuration>\n",
        encoding="utf-8",
    )
    rows = [
        {
            "fileName": package.name,
            "sha256": hashlib.sha256(package.read_bytes()).hexdigest(),
            "sizeBytes": package.stat().st_size,
        }
    ]
    feed_sha = hashlib.sha256(
        json.dumps(rows, sort_keys=True, separators=(",", ":")).encode("utf-8")
    ).hexdigest()
    return {
        "CHUMMER_PUBLISHED_FEED_ROOT": str(feed),
        "CHUMMER_PUBLISHED_FEED_SHA256": feed_sha,
        "CHUMMER_PUBLISHED_FEED_SOURCES": str(feed),
        "CHUMMER_PUBLISHED_NUGET_CONFIG": str(config),
        "CHUMMER_PUBLISHED_NUGET_CONFIG_SHA256": hashlib.sha256(config.read_bytes()).hexdigest(),
    }


def strict_package_plane_environment(tmp_path: Path) -> dict[str, str]:
    environment = os.environ.copy()
    environment.update(strict_feed_environment(tmp_path))
    cache_parent = tmp_path / "strict-package-caches"
    cache_parent.mkdir(mode=0o700)
    environment.update(
        {
            "CHUMMER_PACKAGE_PLANE_SERIALIZE": "0",
            "CHUMMER_STRICT_PACKAGE_CACHE_PARENT": str(cache_parent),
            "CHUMMER_USE_LOCAL_COMPATIBILITY_TREE": "0",
            "CHUMMER_VERIFY_MODE": "release",
            "NUGET_PACKAGES": str(tmp_path / "ambient-nuget-cache"),
        }
    )
    return environment


def test_slice_report_records_mode_and_machine_readable_skip(tmp_path: Path) -> None:
    output = tmp_path / "report.json"
    assert run_contract("start", "--output", output, "--mode", "slice").returncode == 0
    assert (
        run_contract(
            "skip",
            "--output",
            output,
            "--mode",
            "slice",
            "--code",
            "proof.missing",
            "--detail",
            "proof was not supplied",
        ).returncode
        == 0
    )
    assert (
        run_contract(
            "finish",
            "--output",
            output,
            "--mode",
            "slice",
            "--status",
            "passed",
            "--exit-code",
            "0",
        ).returncode
        == 0
    )
    payload = json.loads(output.read_text(encoding="utf-8"))
    assert payload["mode"] == "slice"
    assert payload["status"] == "passed"
    assert payload["skips"] == [
        {
            "code": "proof.missing",
            "detail": "proof was not supplied",
            "recordedAt": payload["skips"][0]["recordedAt"],
            "requiredInRelease": True,
        }
    ]


def test_release_report_cannot_pass_with_skip(tmp_path: Path) -> None:
    output = tmp_path / "report.json"
    assert run_contract("start", "--output", output, "--mode", "release").returncode == 0
    assert (
        run_contract(
            "skip",
            "--output",
            output,
            "--mode",
            "release",
            "--code",
            "proof.missing",
            "--detail",
            "missing",
        ).returncode
        == 0
    )
    result = run_contract(
        "finish",
        "--output",
        output,
        "--mode",
        "release",
        "--status",
        "passed",
        "--exit-code",
        "0",
    )
    assert result.returncode == 2
    assert "cannot pass with skipped proof" in result.stderr


def write_proof(path: Path, *, generated_at: datetime, source_kind: str = "runtime") -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(
            {
                "generatedAt": generated_at.isoformat().replace("+00:00", "Z"),
                "sourceKind": source_kind,
                "status": "passed",
            }
        ),
        encoding="utf-8",
    )


def test_release_input_rejects_stale_and_fixture_proof(tmp_path: Path) -> None:
    stale = tmp_path / "proof.json"
    write_proof(stale, generated_at=datetime.now(UTC) - timedelta(days=2))
    result = run_contract(
        "validate-release-inputs",
        "--proof",
        stale,
        "--manifest-target",
        "https://chummer.run/downloads/releases.json",
        "--max-age-seconds",
        "3600",
    )
    assert result.returncode == 2
    assert "proof is stale" in result.stderr

    fixture = tmp_path / "fresh.json"
    write_proof(fixture, generated_at=datetime.now(UTC), source_kind="fixture")
    result = run_contract(
        "validate-release-inputs",
        "--proof",
        fixture,
        "--manifest-target",
        "https://chummer.run/downloads/releases.json",
    )
    assert result.returncode == 2
    assert "fixture/stub" in result.stderr


def test_implicit_sibling_projects_do_not_enable_local_tree(tmp_path: Path) -> None:
    owner = tmp_path / "owners"
    project_paths = {
        "CHUMMER_LOCAL_CONTRACTS_PROJECT": owner / "core" / "Chummer.Contracts.csproj",
        "CHUMMER_LOCAL_CAMPAIGN_CONTRACTS_PROJECT": owner / "hub" / "Campaign.csproj",
        "CHUMMER_LOCAL_PLAY_CONTRACTS_PROJECT": owner / "hub" / "Play.csproj",
        "CHUMMER_LOCAL_RUN_CONTRACTS_PROJECT": owner / "hub" / "Run.csproj",
        "CHUMMER_LOCAL_HUB_REGISTRY_CONTRACTS_PROJECT": owner / "registry" / "Registry.csproj",
        "CHUMMER_LOCAL_UI_KIT_PROJECT": owner / "kit" / "Kit.csproj",
    }
    for path in project_paths.values():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("<Project />\n", encoding="utf-8")
    environment = os.environ.copy()
    environment.update({key: str(value) for key, value in project_paths.items()})
    environment.pop("CHUMMER_PUBLISHED_FEED_SOURCES", None)
    environment.pop("CHUMMER_USE_LOCAL_COMPATIBILITY_TREE", None)
    environment["CHUMMER_PACKAGE_PLANE_SERIALIZE"] = "0"
    result = subprocess.run(
        ["bash", str(PACKAGE_PLANE), "--info"],
        cwd=REPO_ROOT,
        env=environment,
        text=True,
        capture_output=True,
        check=False,
    )
    assert result.returncode == 2
    assert "no package authority configured" in result.stderr


@pytest.mark.parametrize("mode", ["integration", "release"])
def test_strict_modes_reject_stub_switch(mode: str, tmp_path: Path) -> None:
    report = tmp_path / f"{mode}.json"
    environment = os.environ.copy()
    environment.update(
        {
            "CHUMMER_ALLOW_STUB_PACKAGES": "1",
            "CHUMMER_PUBLISHED_FEED_SOURCES": str(tmp_path / "feed"),
            "CHUMMER_VERIFY_ISOLATED_CACHE_ROOT": str(tmp_path / "new-cache"),
            "CHUMMER_VERIFY_MODE": mode,
            "CHUMMER_VERIFY_REPORT_OUTPUT": str(report),
        }
    )
    result = subprocess.run(
        ["bash", str(VERIFY)],
        cwd=REPO_ROOT,
        env=environment,
        text=True,
        capture_output=True,
        check=False,
    )
    assert result.returncode != 0
    assert "forbids stub packages" in result.stderr
    payload = json.loads(report.read_text(encoding="utf-8"))
    assert payload["mode"] == mode
    assert payload["status"] == "failed"


def test_release_mode_rejects_reused_cache_before_restore(tmp_path: Path) -> None:
    report = tmp_path / "release.json"
    reused = tmp_path / "cache"
    reused.mkdir()
    (reused / "forged.nupkg").write_bytes(b"not a package")
    environment = os.environ.copy()
    environment.update(strict_feed_environment(tmp_path))
    environment.update(
        {
            "CHUMMER_ALLOW_STUB_PACKAGES": "0",
            "CHUMMER_VERIFY_ISOLATED_CACHE_ROOT": str(reused),
            "CHUMMER_VERIFY_MODE": "release",
            "CHUMMER_VERIFY_REPORT_OUTPUT": str(report),
        }
    )
    result = subprocess.run(
        ["bash", str(VERIFY)],
        cwd=REPO_ROOT,
        env=environment,
        text=True,
        capture_output=True,
        check=False,
    )
    assert result.returncode != 0
    assert "requires a new absolute CHUMMER_VERIFY_ISOLATED_CACHE_ROOT" in result.stderr


def test_release_mode_records_missing_proof_as_failing_skip(tmp_path: Path) -> None:
    report = tmp_path / "release.json"
    environment = os.environ.copy()
    environment.update(strict_feed_environment(tmp_path))
    environment.update(
        {
            "CHUMMER_ALLOW_STUB_PACKAGES": "0",
            "CHUMMER_VERIFY_ISOLATED_CACHE_ROOT": str(tmp_path / "new-cache"),
            "CHUMMER_VERIFY_MODE": "release",
            "CHUMMER_VERIFY_REPORT_OUTPUT": str(report),
        }
    )
    result = subprocess.run(
        ["bash", str(VERIFY)],
        cwd=REPO_ROOT,
        env=environment,
        text=True,
        capture_output=True,
        check=False,
    )
    assert result.returncode != 0
    payload = json.loads(report.read_text(encoding="utf-8"))
    assert payload["mode"] == "release"
    assert payload["status"] == "failed"
    assert payload["skips"][0]["code"] == "proof.rule_environment_missing"


def test_release_mode_rejects_disabled_avalonia_primary_route_proof(tmp_path: Path) -> None:
    report = tmp_path / "release.json"
    environment = os.environ.copy()
    environment.update(strict_feed_environment(tmp_path))
    environment.update(
        {
            "CHUMMER_ALLOW_STUB_PACKAGES": "0",
            "CHUMMER_VERIFY_AVALONIA_PRIMARY_ROUTE_PROOF": "0",
            "CHUMMER_VERIFY_ISOLATED_CACHE_ROOT": str(tmp_path / "new-cache"),
            "CHUMMER_VERIFY_MODE": "release",
            "CHUMMER_VERIFY_REPORT_OUTPUT": str(report),
        }
    )
    result = subprocess.run(
        ["bash", str(VERIFY)], cwd=REPO_ROOT, env=environment, text=True, capture_output=True, check=False
    )
    assert result.returncode != 0
    assert "release verification cannot skip" in result.stderr
    payload = json.loads(report.read_text(encoding="utf-8"))
    assert payload["skips"][0]["code"] == "proof.avalonia_primary_route_disabled"


def test_strict_feed_rejects_ambient_source_and_digest_drift(tmp_path: Path) -> None:
    authority = strict_feed_environment(tmp_path)
    config = Path(authority["CHUMMER_PUBLISHED_NUGET_CONFIG"])
    config.write_text(
        config.read_text(encoding="utf-8").replace(
            "    <clear />\n", "    <clear />\n    <add key=\"ambient\" value=\"https://api.nuget.org/v3/index.json\" />\n"
        ),
        encoding="utf-8",
    )
    result = run_contract(
        "validate-feed-authority",
        "--config",
        config,
        "--feed-root",
        authority["CHUMMER_PUBLISHED_FEED_ROOT"],
        "--config-sha256",
        hashlib.sha256(config.read_bytes()).hexdigest(),
        "--feed-sha256",
        authority["CHUMMER_PUBLISHED_FEED_SHA256"],
    )
    assert result.returncode == 2
    assert "packageSources is not exact" in result.stderr

    package = Path(authority["CHUMMER_PUBLISHED_FEED_ROOT"]) / "Pinned.1.0.0.nupkg"
    package.write_bytes(b"mutated")
    result = run_contract(
        "validate-feed-authority",
        "--config",
        config,
        "--feed-root",
        authority["CHUMMER_PUBLISHED_FEED_ROOT"],
        "--config-sha256",
        hashlib.sha256(config.read_bytes()).hexdigest(),
        "--feed-sha256",
        authority["CHUMMER_PUBLISHED_FEED_SHA256"],
    )
    assert result.returncode == 2
    assert "feed inventory digest differs" in result.stderr


@pytest.mark.parametrize(
    "argument",
    [
        "--no-restore",
        "--no-build",
        "--no-dependencies",
        "/p:Restore=false",
        "-p:Restore=false;ContinuousIntegrationBuild=true",
        "-p:RestorePackagesPath=/tmp/caller-cache",
        "-p:VSTestNoBuild=true",
        "-p:CustomAfterMicrosoftCommonTargets=/tmp/forged.targets",
        "-p:CustomBeforeMicrosoftCommonProps=/tmp/forged.props",
        "-p:DirectoryBuildTargetsPath=/tmp/forged.targets",
        "-p:ImportByWildcardAfterMicrosoftCommonTargets=true",
        "-p:MSBuildProjectExtensionsPath=/tmp/forged-obj",
        "--property:RestoreSources=https://packages.invalid/v3/index.json",
        "@hidden-restore-options.rsp",
    ],
)
def test_strict_package_plane_rejects_preexisting_restore_or_build_outputs(
    tmp_path: Path, argument: str
) -> None:
    result = subprocess.run(
        ["bash", str(PACKAGE_PLANE), "build", "Chummer.Presentation/Chummer.Presentation.csproj", argument],
        cwd=REPO_ROOT,
        env=strict_package_plane_environment(tmp_path),
        text=True,
        capture_output=True,
        check=False,
    )
    assert result.returncode == 2
    assert "rejects caller-supplied" in result.stderr


def test_strict_package_plane_rehashes_feed_after_dotnet_returns(tmp_path: Path) -> None:
    environment = strict_package_plane_environment(tmp_path)
    fake_bin = tmp_path / "bin"
    fake_bin.mkdir()
    fake_dotnet = fake_bin / "dotnet"
    fake_dotnet.write_text(
        "#!/usr/bin/env bash\n"
        "set -euo pipefail\n"
        'printf mutated > "$CHUMMER_PUBLISHED_FEED_ROOT/Pinned.1.0.0.nupkg"\n',
        encoding="utf-8",
    )
    fake_dotnet.chmod(0o700)
    environment["PATH"] = f"{fake_bin}:{environment['PATH']}"

    result = subprocess.run(
        ["bash", str(PACKAGE_PLANE), "--info"],
        cwd=REPO_ROOT,
        env=environment,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 2
    assert "feed inventory digest differs" in result.stderr


@pytest.mark.parametrize(
    "environment_name",
    [
        "CustomAfterMicrosoftCommonTargets",
        "CustomBeforeMicrosoftCommonProps",
        "DirectoryBuildPropsPath",
        "ImportByWildcardBeforeMicrosoftCommonTargets",
        "MSBuildProjectExtensionsPath",
    ],
)
def test_strict_package_plane_rejects_ambient_msbuild_import_authority(
    tmp_path: Path, environment_name: str
) -> None:
    environment = strict_package_plane_environment(tmp_path)
    environment[environment_name] = str(tmp_path / "forged.targets")
    result = subprocess.run(
        ["bash", str(PACKAGE_PLANE), "--info"],
        cwd=REPO_ROOT,
        env=environment,
        text=True,
        capture_output=True,
        check=False,
    )
    assert result.returncode == 2
    assert "rejects ambient MSBuild import/property authority" in result.stderr


def test_strict_verify_uses_conventional_dotnet_test_path() -> None:
    source = VERIFY.read_text(encoding="utf-8")
    assert (
        "bash scripts/ai/with-package-plane.sh test \\\n"
        "    Chummer.Product.UnitTests/Chummer.Product.UnitTests.csproj"
    ) in source


def test_explicit_slice_local_tree_is_machine_readable(tmp_path: Path) -> None:
    report = tmp_path / "slice.json"
    environment = os.environ.copy()
    environment.update(
        {
            "CHUMMER_USE_LOCAL_COMPATIBILITY_TREE": "1",
            "CHUMMER_VERIFY_AVALONIA_PRIMARY_ROUTE_PROOF": "invalid",
            "CHUMMER_VERIFY_MODE": "slice",
            "CHUMMER_VERIFY_REPORT_OUTPUT": str(report),
        }
    )
    result = subprocess.run(
        ["bash", str(VERIFY)], cwd=REPO_ROOT, env=environment, text=True, capture_output=True, check=False
    )
    assert result.returncode != 0
    payload = json.loads(report.read_text(encoding="utf-8"))
    assert payload["skips"][0]["code"] == "package_plane.local_tree"
