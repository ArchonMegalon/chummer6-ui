from __future__ import annotations

import importlib.util
import hashlib
import io
import json
import os
import shutil
import stat
import subprocess
import textwrap
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path
from types import ModuleType

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
LOCK = REPO_ROOT / "config" / "package-plane.lock.json"


def load_module() -> ModuleType:
    spec = importlib.util.spec_from_file_location("fresh_package_plane", SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


package_plane = load_module()


def test_checked_in_lock_and_consumer_source_digests_are_current() -> None:
    lock = package_plane.load_json(LOCK)
    package_plane.validate_lock(lock)
    package_plane.validate_test_compile_items(REPO_ROOT)
    rows = package_plane.verify_source_files(REPO_ROOT, lock["consumer"]["sourceFiles"])
    assert len(rows) == len(lock["consumer"]["sourceFiles"])


def test_forged_owner_pin_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["owners"][0]["commit"] = "main"
    with pytest.raises(package_plane.VerificationError, match="owner commit is not exact"):
        package_plane.validate_lock(lock)


def test_well_formed_but_substituted_owner_authority_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["owners"][0]["commit"] = "f" * 40
    with pytest.raises(package_plane.VerificationError, match="fixed authority"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["owners"][0]["repository"] = "https://github.com/ArchonMegalon/substitute.git"
    with pytest.raises(package_plane.VerificationError, match="fixed authority"):
        package_plane.validate_lock(lock)


def test_substituted_hub_canonical_feed_authority_is_rejected(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["canonicalOwnerFeed"]["packages"][0]["sha256"] = "f" * 64
    with pytest.raises(package_plane.VerificationError, match="canonical package authority"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["canonicalOwnerFeed"]["producerCommit"] = "f" * 40
    with pytest.raises(package_plane.VerificationError, match="fixed feed"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    engine = lock["canonicalOwnerFeed"]["packages"][0]
    overlapping_engine = {
        "fileName": engine["fileName"],
        "ownerDirectory": "chummer-core-engine",
        "packageId": engine["packageId"],
        "project": engine["project"],
        "version": engine["version"],
    }
    lock["packages"].append(overlapping_engine)
    expected_packages = dict(package_plane.EXPECTED_PACKAGES)
    expected_packages["Chummer.Engine.Contracts"] = (
        overlapping_engine["ownerDirectory"],
        overlapping_engine["project"],
        overlapping_engine["fileName"],
        overlapping_engine["version"],
    )
    monkeypatch.setattr(package_plane, "EXPECTED_PACKAGES", expected_packages)
    with pytest.raises(package_plane.VerificationError, match="UI-owned and Hub canonical"):
        package_plane.validate_lock(lock)


def test_canonical_and_ui_package_planes_are_exact_atomic_and_disjoint() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    canonical = lock["canonicalOwnerFeed"]
    canonical_ids = {row["packageId"] for row in canonical["packages"]}
    ui_ids = {row["packageId"] for row in lock["packages"]}

    assert canonical["lockContract"] == "chummer-hub.package-plane-lock/v4"
    assert canonical["inventoryContract"] == "chummer-hub.external-package-inventory/v3"
    assert canonical_ids == {
        "Chummer.Engine.Contracts",
        "Chummer.Engine.GmCharacterEdits",
        "Chummer.Hub.Registry.Contracts",
        "Chummer.Play.Contracts",
        "Chummer.Run.Contracts",
        "Chummer.Run.Registry",
    }
    assert ui_ids == {
        "Chummer.Application",
        "Chummer.Campaign.Contracts",
        "Chummer.Infrastructure",
        "Chummer.Rulesets.Hosting",
        "Chummer.Rulesets.Sr4",
        "Chummer.Rulesets.Sr5",
        "Chummer.Rulesets.Sr6",
        "Chummer.Ui.Kit",
    }
    assert canonical_ids.isdisjoint(ui_ids)
    assert len(canonical_ids | ui_ids) == 14
    assert all(
        {"repository", "commit", "project"}.issubset(row)
        for row in canonical["packages"]
    )

    current = lock["currentOwnerContractFeed"]
    assert {row["packageId"] for row in current["packages"]} == {
        "Chummer.Engine.Contracts",
        "Chummer.Hub.Registry.Contracts",
        "Chummer.Play.Contracts",
        "Chummer.Run.Contracts",
    }
    current_receipt = package_plane.current_owner_contract_feed_binding_receipt(lock)
    assert current_receipt["selectedForCanonicalFullFeed"] is False
    assert current_receipt["status"] == "bound_not_selected"

    assert lock["canonicalOwnerFeed"]["producerCommit"] == (
        "dc5af2be14af958f071f957a537b7f61e6d4fd09"
    )
    assert LOCK.read_text(encoding="utf-8").count(
        "dc5af2be14af958f071f957a537b7f61e6d4fd09"
    ) == 1
    assert "3b72367cc13e76d3d50db9eeec3224785037fb5e" not in SCRIPT.read_text(
        encoding="utf-8"
    )


def test_mutable_external_package_source_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["externalPackages"][0]["source"] = "https://api.nuget.org/v3/index.json"
    with pytest.raises(package_plane.VerificationError, match="immutable NuGet path"):
        package_plane.validate_lock(lock)


def test_missing_core_runtime_package_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["packages"][-1]["packageId"] = "Chummer.Rulesets.Sr7"
    with pytest.raises(package_plane.VerificationError, match="fixed package set"):
        package_plane.validate_lock(lock)


def test_reduced_consumer_digest_or_build_set_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["consumer"]["sourceFiles"].pop("Chummer.Avalonia/Chummer.Avalonia.csproj")
    with pytest.raises(package_plane.VerificationError, match="source-file set"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["consumer"]["buildProjects"].pop()
    with pytest.raises(package_plane.VerificationError, match="build project set"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["consumer"]["testProjects"].pop()
    with pytest.raises(package_plane.VerificationError, match="test project set"):
        package_plane.validate_lock(lock)


def test_product_unit_test_compile_set_rejects_an_extra_source(tmp_path: Path) -> None:
    project_dir = tmp_path / "Chummer.Product.UnitTests"
    project_dir.mkdir()
    source = (REPO_ROOT / "Chummer.Product.UnitTests" / "Chummer.Product.UnitTests.csproj").read_text(
        encoding="utf-8"
    )
    (project_dir / "Chummer.Product.UnitTests.csproj").write_text(
        source.replace(
            "</Project>",
            '  <ItemGroup><Compile Include="InjectedTests.cs" /></ItemGroup>\n</Project>',
        ),
        encoding="utf-8",
    )
    with pytest.raises(package_plane.VerificationError, match="compile source set"):
        package_plane.validate_test_compile_items(tmp_path)

    (project_dir / "Chummer.Product.UnitTests.csproj").write_text(
        source.replace(
            "<EnableDefaultCompileItems>false</EnableDefaultCompileItems>",
            "<EnableDefaultCompileItems Condition=\"'$(Injected)' == '1'\">false</EnableDefaultCompileItems>",
        ),
        encoding="utf-8",
    )
    with pytest.raises(package_plane.VerificationError, match="disable default compile globs"):
        package_plane.validate_test_compile_items(tmp_path)

    (project_dir / "Chummer.Product.UnitTests.csproj").write_text(
        source.replace(
            "  <ItemGroup>\n    <Compile Include=\"DesktopUpdateArtifactTests.cs\" />",
            "  <ItemGroup Condition=\"'$(Injected)' == '1'\">\n"
            "    <Compile Include=\"DesktopUpdateArtifactTests.cs\" />",
        ),
        encoding="utf-8",
    )
    with pytest.raises(package_plane.VerificationError, match="must be unconditional"):
        package_plane.validate_test_compile_items(tmp_path)


def test_child_environment_drops_ambient_msbuild_nuget_and_chummer_inputs(
    tmp_path: Path,
) -> None:
    malicious = tmp_path / "malicious-path"
    malicious.mkdir()
    malicious_marker = tmp_path / "malicious-executed"
    for name in ("bash", "dotnet", "git", "python3"):
        executable = malicious / name
        executable.write_text(
            f"#!/bin/sh\nprintf hit > '{malicious_marker}'\nexit 99\n",
            encoding="utf-8",
        )
        executable.chmod(0o700)
    trusted_dotnet_root = tmp_path / "trusted-dotnet"
    trusted_dotnet_root.mkdir()
    trusted_dotnet = trusted_dotnet_root / "dotnet"
    trusted_dotnet.write_text("#!/bin/sh\nexit 0\n", encoding="utf-8")
    trusted_dotnet.chmod(0o700)
    parent = {
        "PATH": f"{malicious}:{os.environ['PATH']}",
        "HTTP_PROXY": "http://network-proxy.invalid:8080",
        "DirectoryBuildPropsPath": "/tmp/injected.props",
        "CustomBeforeMicrosoftCommonTargets": "/tmp/injected.targets",
        "MSBuildSDKsPath": "/tmp/injected-sdks",
        "RestoreSources": "https://packages.invalid/v3/index.json",
        "CHUMMER_CONTRACTS_PACKAGE_VERSION": "999.0.0",
        "NUGET_PACKAGES": "/tmp/ambient-packages",
        "NUGET_CREDENTIALPROVIDERS_PATH": "/tmp/injected-provider",
        "BASH_ENV": "/tmp/injected-bash-env",
        "LD_PRELOAD": "/tmp/injected.so",
    }

    environment = package_plane.isolated_child_environment(
        tmp_path / "caches",
        parent,
        trusted_dotnet_root=trusted_dotnet_root,
    )

    assert environment["HTTP_PROXY"] == parent["HTTP_PROXY"]
    assert environment["PATH"] == (
        f"{trusted_dotnet_root}:{package_plane.TRUSTED_SYSTEM_PATH}"
    )
    assert str(malicious) not in environment["PATH"]
    assert subprocess.run(
        ["dotnet", "--version"],
        env=environment,
        check=False,
    ).returncode == 0
    assert not malicious_marker.exists()
    assert Path(environment["NUGET_PACKAGES"]).is_relative_to(tmp_path)
    for name in (
        "DirectoryBuildPropsPath",
        "CustomBeforeMicrosoftCommonTargets",
        "MSBuildSDKsPath",
        "RestoreSources",
        "CHUMMER_CONTRACTS_PACKAGE_VERSION",
        "NUGET_CREDENTIALPROVIDERS_PATH",
        "BASH_ENV",
        "LD_PRELOAD",
    ):
        assert name not in environment


def test_owner_pack_and_consumer_restore_reject_version_approximation() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    assert "-p:ChummerHubRegistryContractsPackageVersion=0.1.0-preview" in source
    assert "-p:ChummerRunContractsPackageVersion=0.1.0-preview" in source
    assert "-p:ChummerRunRegistryPackageVersion=0.1.0-preview" in source
    assert (
        "-p:ChummerEngineContractsPackageVersion="
        "{CANONICAL_ENGINE_CONTRACTS_VERSION}" in source
    )
    assert "-p:ChummerLocalContractsProject=" in source
    assert "-p:ChummerUseLocalCompatibilityTree=false" in source
    assert "-p:RestoreLockedMode=false" not in source
    assert "-p:RestorePackagesWithLockFile=false" not in source
    assert source.count("-p:RestoreLockedMode=true") == 1
    assert "canonical_feed_receipt = import_hub_canonical_feed(" in source
    assert "if package[\"packageId\"] in HUB_CANONICAL_PACKAGE_IDS:" not in source
    assert source.count("-warnaserror:NU1603,NU1608") == 3
    assert source.count("-p:WarningsAsErrors=NU1603%3BNU1608") == 1
    assert source.count('"--minimum-expected-tests"') == 1
    assert source.count('"--no-progress"') == 1
    for authority in (
        "-p:RestoreSources={feed}",
        "-p:RestoreAdditionalProjectSources=",
        "-p:RestoreConfigFile={pack_config}",
        "-p:RestoreFallbackFolders=",
        "-p:RestoreIgnoreFailedSources=false",
    ):
        assert authority in source

    props = (REPO_ROOT / "Directory.Build.props").read_text(encoding="utf-8")
    helper = (REPO_ROOT / "scripts" / "ai" / "with-package-plane.sh").read_text(
        encoding="utf-8"
    )
    assert (
        "<ChummerContractsPackageVersion Condition=\"'$(ChummerContractsPackageVersion)' == ''\">"
        "0.0.0-packageplane.candidate.sha0612fb3ebf2b"
        "</ChummerContractsPackageVersion>"
    ) in props
    assert 'configured_contracts_version="${CHUMMER_CONTRACTS_PACKAGE_VERSION:-}"' in helper
    assert (
        'contracts_version="${configured_contracts_version:-'
        '0.0.0-packageplane.candidate.sha0612fb3ebf2b}"' in helper
    )
    assert (
        "'-p:NuGetLockFilePath=$(BaseIntermediateOutputPath)"
        "packages.local-tree.lock.json'"
    ) in helper
    assert "5.225.0.0" not in props
    assert "5.225.0.0" not in helper


def test_local_source_graph_uses_locked_owner_packages_once() -> None:
    props = (REPO_ROOT / "Directory.Build.props").read_text(encoding="utf-8")
    helper = (REPO_ROOT / "scripts" / "ai" / "with-package-plane.sh").read_text(
        encoding="utf-8"
    )
    assert (
        "<ChummerUseLockedOwnerContractPackages "
        "Condition=\"'$(ChummerUseLockedOwnerContractPackages)' == ''\">"
        "false</ChummerUseLockedOwnerContractPackages>"
    ) in props
    assert "-p:ChummerUseLockedOwnerContractPackages=true" in helper
    assert "bootstrap-owner-contracts-feed.py" in helper
    assert "--print-version" in helper
    assert "--validate-only" in helper
    assert (
        'CHUMMER_ENGINE_CONTRACTS_PACKAGE_VERSION="$owner_contracts_package_version"'
        in helper
    )

    consumer_projects = (
        "Chummer.Presentation/Chummer.Presentation.csproj",
        "Chummer.Desktop.Runtime/Chummer.Desktop.Runtime.csproj",
        "Chummer.Avalonia/Chummer.Avalonia.csproj",
        "Chummer.Blazor/Chummer.Blazor.csproj",
        "Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj",
    )
    for relative_path in consumer_projects:
        root = ET.parse(REPO_ROOT / relative_path).getroot()
        local_run_conditions: list[str] = []
        locked_run_conditions: list[str] = []
        local_engine_references = 0
        for group in root.findall("ItemGroup"):
            group_condition = group.attrib.get("Condition", "")
            for reference in group:
                include = reference.attrib.get("Include")
                effective_condition = " ".join(
                    (group_condition, reference.attrib.get("Condition", ""))
                )
                if reference.tag == "ProjectReference" and include == "$(ChummerLocalContractsProject)":
                    local_engine_references += 1
                if reference.tag == "ProjectReference" and include == "$(ChummerLocalRunContractsProject)":
                    local_run_conditions.append(effective_condition)
                if reference.tag == "PackageReference" and include == "$(ChummerRunContractsPackageId)":
                    if "ChummerUseLockedOwnerContractPackages" in effective_condition:
                        locked_run_conditions.append(effective_condition)

        assert local_engine_references == 1, relative_path
        assert len(local_run_conditions) == 1, relative_path
        assert all(
            "'$(ChummerUseLockedOwnerContractPackages)' != 'true'" in condition
            for condition in local_run_conditions
        ), relative_path
        assert len(locked_run_conditions) == 1, relative_path
        assert all(
            "'$(ChummerUseLocalCompatibilityTree)' == 'true'" in condition
            and "'$(ChummerUseLockedOwnerContractPackages)' == 'true'" in condition
            for condition in locked_run_conditions
        ), relative_path

    desktop_root = ET.parse(
        REPO_ROOT / "Chummer.Desktop.Runtime" / "Chummer.Desktop.Runtime.csproj"
    ).getroot()
    local_registry_conditions: list[str] = []
    locked_registry_conditions: list[str] = []
    for group in desktop_root.findall("ItemGroup"):
        group_condition = group.attrib.get("Condition", "")
        for reference in group:
            effective_condition = " ".join(
                (group_condition, reference.attrib.get("Condition", ""))
            )
            if (
                reference.tag == "ProjectReference"
                and reference.attrib.get("Include") == "$(ChummerLocalHubRegistryContractsProject)"
            ):
                local_registry_conditions.append(effective_condition)
            if (
                reference.tag == "PackageReference"
                and reference.attrib.get("Include") == "$(ChummerHubRegistryContractsPackageId)"
                and "ChummerUseLockedOwnerContractPackages" in effective_condition
            ):
                locked_registry_conditions.append(effective_condition)
    assert len(local_registry_conditions) == 1
    assert "'$(ChummerUseLockedOwnerContractPackages)' != 'true'" in local_registry_conditions[0]
    assert len(locked_registry_conditions) == 1
    assert "'$(ChummerUseLockedOwnerContractPackages)' == 'true'" in locked_registry_conditions[0]


def _write_restore_project(
    path: Path,
    body: str = "",
    properties: str = "",
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
        "  <PropertyGroup>\n"
        "    <TargetFramework>net10.0</TargetFramework>\n"
        "    <Version>0.0.0-local</Version>\n"
        f"{properties}"
        "  </PropertyGroup>\n"
        f"{body}"
        "</Project>\n",
        encoding="utf-8",
    )


def _write_owner_contract_package(
    feed: Path,
    package_id: str,
    version: str,
    dependencies: tuple[str, ...] = (),
) -> dict[str, object]:
    dependency_rows = "".join(
        f'        <dependency id="{dependency}" version="[{version}]" />\n'
        for dependency in dependencies
    )
    package_path = feed / f"{package_id}.{version}.nupkg"
    nuspec = (
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        "<package>\n"
        "  <metadata>\n"
        f"    <id>{package_id}</id>\n"
        f"    <version>{version}</version>\n"
        "    <authors>test</authors>\n"
        "    <description>Owner-contract restore fixture.</description>\n"
        "    <dependencies>\n"
        "      <group targetFramework=\"net10.0\">\n"
        f"{dependency_rows}"
        "      </group>\n"
        "    </dependencies>\n"
        "  </metadata>\n"
        "</package>\n"
    )
    with zipfile.ZipFile(package_path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec)
        archive.writestr(f"lib/net10.0/{package_id}.dll", b"restore-fixture")
    return {
        "id": package_id,
        "version": version,
        "file_name": package_path.name,
        "sha256": hashlib.sha256(package_path.read_bytes()).hexdigest(),
    }


def test_local_locked_owner_restore_derives_version_and_uses_one_source(
    tmp_path: Path,
) -> None:
    dotnet = shutil.which("dotnet")
    assert dotnet is not None
    owner_version = "0.0.0-packageplane.20260721.1"
    owners_root = tmp_path / "owners"
    core_root = owners_root / "core"
    hub_root = owners_root / "hub"
    registry_root = owners_root / "registry"
    ui_kit_root = owners_root / "ui-kit"
    contracts_project = core_root / "Chummer.Contracts" / "Chummer.Contracts.csproj"
    campaign_project = (
        hub_root / "Chummer.Campaign.Contracts" / "Chummer.Campaign.Contracts.csproj"
    )
    play_project = hub_root / "Chummer.Play.Contracts" / "Chummer.Play.Contracts.csproj"
    run_project = hub_root / "Chummer.Run.Contracts" / "Chummer.Run.Contracts.csproj"
    registry_project = (
        registry_root
        / "Chummer.Hub.Registry.Contracts"
        / "Chummer.Hub.Registry.Contracts.csproj"
    )
    ui_kit_project = ui_kit_root / "src" / "Chummer.Ui.Kit" / "Chummer.Ui.Kit.csproj"
    _write_restore_project(
        contracts_project,
        properties=(
            "    <PackageId>Chummer.Engine.Contracts</PackageId>\n"
            "    <AssemblyName>Chummer.Engine.Contracts</AssemblyName>\n"
        ),
    )
    for project in (
        campaign_project,
        play_project,
        run_project,
        registry_project,
        ui_kit_project,
    ):
        _write_restore_project(project)

    feed = tmp_path / "owner-feed"
    feed.mkdir()
    package_rows = [
        _write_owner_contract_package(feed, "Chummer.Engine.Contracts", owner_version),
        _write_owner_contract_package(
            feed, "Chummer.Hub.Registry.Contracts", owner_version
        ),
        _write_owner_contract_package(feed, "Chummer.Play.Contracts", owner_version),
        _write_owner_contract_package(
            feed,
            "Chummer.Run.Contracts",
            owner_version,
            (
                "Chummer.Engine.Contracts",
                "Chummer.Hub.Registry.Contracts",
                "Chummer.Play.Contracts",
            ),
        ),
    ]
    inventory = {
        "contract": "chummer-core.owner-contract-package-inventory/v1",
        "package_plane_lock_sha256": "0" * 64,
        "package_version": owner_version,
        "packages": package_rows,
    }
    inventory_path = feed / "chummer-owner-contracts.inventory.json"
    inventory_path.write_text(json.dumps(inventory, indent=2) + "\n", encoding="utf-8")

    validation_marker = tmp_path / "validation.marker"
    owner_helper = core_root / "scripts" / "ai" / "bootstrap-owner-contracts-feed.py"
    owner_helper.parent.mkdir(parents=True)
    owner_helper.write_text(
        textwrap.dedent(
            """\
            #!/usr/bin/env python3
            import argparse
            import hashlib
            import json
            import os
            from pathlib import Path

            parser = argparse.ArgumentParser()
            parser.add_argument("--repo-root", required=True)
            parser.add_argument("--feed")
            parser.add_argument("--print-version", action="store_true")
            parser.add_argument("--validate-only", action="store_true")
            args = parser.parse_args()
            version = os.environ["EXPECTED_OWNER_CONTRACTS_VERSION"]
            if args.print_version:
                print(version)
                raise SystemExit(0)
            if not args.validate_only or not args.feed:
                raise SystemExit("expected --validate-only with an exact feed")
            feed = Path(args.feed).resolve()
            inventory_path = feed / "chummer-owner-contracts.inventory.json"
            payload = json.loads(inventory_path.read_text(encoding="utf-8"))
            expected_ids = (
                "Chummer.Engine.Contracts",
                "Chummer.Hub.Registry.Contracts",
                "Chummer.Play.Contracts",
                "Chummer.Run.Contracts",
            )
            if payload.get("contract") != "chummer-core.owner-contract-package-inventory/v1":
                raise SystemExit("inventory contract mismatch")
            if payload.get("package_version") != version:
                raise SystemExit("inventory version mismatch")
            rows = payload.get("packages")
            if not isinstance(rows, list) or tuple(row.get("id") for row in rows) != expected_ids:
                raise SystemExit("inventory package set mismatch")
            expected_files = {inventory_path.name}
            for row in rows:
                if row.get("version") != version:
                    raise SystemExit("inventory package version mismatch")
                package = feed / row["file_name"]
                expected_files.add(package.name)
                if hashlib.sha256(package.read_bytes()).hexdigest() != row.get("sha256"):
                    raise SystemExit("inventory package digest mismatch")
            if {path.name for path in feed.iterdir()} != expected_files:
                raise SystemExit("feed contains missing or unexpected entries")
            Path(os.environ["OWNER_VALIDATION_MARKER"]).write_text(version, encoding="utf-8")
            """
        ),
        encoding="utf-8",
    )

    bootstrap_marker = tmp_path / "bootstrap.marker"
    engine_bootstrap = tmp_path / "bootstrap-contracts-feed.sh"
    engine_bootstrap.write_text(
        "#!/usr/bin/env bash\n"
        "set -euo pipefail\n"
        'test "$CHUMMER_ENGINE_CONTRACTS_PACKAGE_VERSION" = '
        '"$EXPECTED_OWNER_CONTRACTS_VERSION"\n'
        'printf "%s" "$CHUMMER_ENGINE_CONTRACTS_PACKAGE_VERSION" > '
        '"$OWNER_BOOTSTRAP_MARKER"\n',
        encoding="utf-8",
    )
    engine_bootstrap.chmod(0o700)

    consumer = tmp_path / "consumer" / "OwnerGraph.Consumer.csproj"
    _write_restore_project(
        consumer,
        (
            "  <ItemGroup Condition=\"'$(ChummerUseLocalCompatibilityTree)' == 'true'\">\n"
            f"    <ProjectReference Include=\"{contracts_project.as_posix()}\" />\n"
            f"    <ProjectReference Include=\"{run_project.as_posix()}\" "
            "Condition=\"'$(ChummerUseLockedOwnerContractPackages)' != 'true'\" />\n"
            f"    <ProjectReference Include=\"{registry_project.as_posix()}\" "
            "Condition=\"'$(ChummerUseLockedOwnerContractPackages)' != 'true'\" />\n"
            "  </ItemGroup>\n"
            "  <ItemGroup Condition=\"'$(ChummerUseLocalCompatibilityTree)' == 'true' "
            "and '$(ChummerUseLockedOwnerContractPackages)' == 'true'\">\n"
            "    <PackageReference Include=\"Chummer.Run.Contracts\" "
            "Version=\"$(ChummerRunContractsPackageVersion)\" />\n"
            "    <PackageReference Include=\"Chummer.Hub.Registry.Contracts\" "
            "Version=\"$(ChummerHubRegistryContractsPackageVersion)\" />\n"
            "  </ItemGroup>\n"
        ),
    )
    nuget_config = tmp_path / "NuGet.Config"
    nuget_config.write_text(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        "<configuration><packageSources><clear /></packageSources></configuration>\n",
        encoding="utf-8",
    )

    environment = os.environ.copy()
    for name in tuple(environment):
        if name.startswith("CHUMMER_") or name in {
            "NUGET_PACKAGES",
            "RestoreSources",
            "RestoreAdditionalProjectSources",
            "RestoreConfigFile",
        }:
            environment.pop(name, None)
    environment.update(
        {
            "CHUMMER_VERIFY_MODE": "slice",
            "CHUMMER_USE_LOCAL_COMPATIBILITY_TREE": "1",
            "CHUMMER_PACKAGE_PLANE_SERIALIZE": "0",
            "CHUMMER_LOCAL_CONTRACTS_PROJECT": str(contracts_project),
            "CHUMMER_LOCAL_CAMPAIGN_CONTRACTS_PROJECT": str(campaign_project),
            "CHUMMER_LOCAL_PLAY_CONTRACTS_PROJECT": str(play_project),
            "CHUMMER_LOCAL_RUN_CONTRACTS_PROJECT": str(run_project),
            "CHUMMER_LOCAL_HUB_REGISTRY_CONTRACTS_PROJECT": str(registry_project),
            "CHUMMER_LOCAL_UI_KIT_PROJECT": str(ui_kit_project),
            "CHUMMER_BOOTSTRAP_ENGINE_CONTRACTS_SCRIPT": str(engine_bootstrap),
            "CHUMMER_ENGINE_CONTRACTS_FEED": str(feed),
            "CHUMMER_PACKAGE_PLANE_LOCK_ROOT": str(tmp_path / "locks"),
            "NUGET_PACKAGES": str(tmp_path / "nuget-packages"),
            "DOTNET_CLI_HOME": str(tmp_path / "dotnet-home"),
            "EXPECTED_OWNER_CONTRACTS_VERSION": owner_version,
            "OWNER_BOOTSTRAP_MARKER": str(bootstrap_marker),
            "OWNER_VALIDATION_MARKER": str(validation_marker),
        }
    )
    command = [
        "bash",
        str(REPO_ROOT / "scripts" / "ai" / "with-package-plane.sh"),
        "restore",
        str(consumer),
        "--configfile",
        str(nuget_config),
        "--no-cache",
    ]
    completed = subprocess.run(
        command,
        cwd=REPO_ROOT,
        env=environment,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    assert completed.returncode == 0, completed.stdout
    assert bootstrap_marker.read_text(encoding="utf-8") == owner_version
    assert validation_marker.read_text(encoding="utf-8") == owner_version

    assets = json.loads(
        (consumer.parent / "obj" / "project.assets.json").read_text(encoding="utf-8")
    )
    libraries = assets["libraries"]
    expected_identities = {
        "Chummer.Engine.Contracts": "project",
        "Chummer.Hub.Registry.Contracts": "package",
        "Chummer.Play.Contracts": "package",
        "Chummer.Run.Contracts": "package",
    }
    for package_id, expected_type in expected_identities.items():
        matches = [
            (identity, row)
            for identity, row in libraries.items()
            if identity.startswith(f"{package_id}/")
        ]
        assert len(matches) == 1, (package_id, matches)
        identity, row = matches[0]
        assert row["type"] == expected_type, identity
        if expected_type == "package":
            assert identity == f"{package_id}/{owner_version}"
    assert set(assets["project"]["restore"]["sources"]) == {str(feed.resolve())}
    assert not assets.get("logs")

    conflict_environment = dict(environment)
    conflict_environment["CHUMMER_RUN_CONTRACTS_PACKAGE_VERSION"] = "0.1.0-preview"
    conflict = subprocess.run(
        command,
        cwd=REPO_ROOT,
        env=conflict_environment,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    assert conflict.returncode == 2
    assert (
        "CHUMMER_RUN_CONTRACTS_PACKAGE_VERSION must equal the exact Core "
        f"owner-contract package version {owner_version}."
    ) in conflict.stdout

    inventory["packages"][0]["sha256"] = "f" * 64
    inventory_path.write_text(json.dumps(inventory, indent=2) + "\n", encoding="utf-8")
    invalid_environment = dict(environment)
    invalid_environment["CHUMMER_BOOTSTRAP_ENGINE_CONTRACTS_FEED"] = "0"
    invalid_inventory = subprocess.run(
        command,
        cwd=REPO_ROOT,
        env=invalid_environment,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    assert invalid_inventory.returncode == 2
    assert "Core owner-contract package inventory validation failed." in invalid_inventory.stdout


def test_private_sdk_and_every_execution_are_bound_to_exact_program_version() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    assert "sdk_root, sdk_archive_sha512 = acquire_sdk(" in source
    assert "owner_sdk_versions" in source
    assert '"sdkArchiveSha512": sdk_archive_sha512' in source
    assert '"buildExecutions": build_executions' in source
    assert '"testExecutions": test_executions' in source
    assert '"contractVersion": 8' in source
    assert "command = [\n        str(TRUSTED_PYTHON3)," in source
    assert "sys.executable" not in source
    assert '"canonicalOwnerFeed": canonical_feed_receipt' in source
    assert '"projectLockFilesEnforced": True' in source


def test_sdk_archive_authority_is_exact() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["sdkArchive"]["sha512"] = "f" * 128
    with pytest.raises(package_plane.VerificationError, match="SDK version differs"):
        package_plane.validate_lock(lock)


def test_extra_package_or_external_source_row_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["packages"].append(dict(lock["packages"][0]))
    with pytest.raises(package_plane.VerificationError, match="cardinality"):
        package_plane.validate_lock(lock)

    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["externalPackages"][0]["sha256"] = "f" * 64
    with pytest.raises(package_plane.VerificationError, match="fixed package/source set"):
        package_plane.validate_lock(lock)


def test_changed_consumer_source_is_rejected(tmp_path: Path) -> None:
    source = tmp_path / "Directory.Build.props"
    source.write_text("trusted\n", encoding="utf-8")
    locked = {"Directory.Build.props": package_plane.source_digest(source)}
    source.write_text("tampered\n", encoding="utf-8")
    with pytest.raises(package_plane.VerificationError, match="differs from package-plane lock"):
        package_plane.verify_source_files(tmp_path, locked)


def write_package(path: Path, content: bytes) -> None:
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("Package.nuspec", "<package />")
        archive.writestr("lib/net10.0/Package.dll", content)


def test_tampered_nupkg_reuse_changes_cryptographic_inventory(tmp_path: Path) -> None:
    package = tmp_path / "Package.1.0.0.nupkg"
    write_package(package, b"original")
    before = package_plane.package_inventory(tmp_path, {package.name})
    write_package(package, b"forged")
    after = package_plane.package_inventory(tmp_path, {package.name})
    with pytest.raises(package_plane.VerificationError, match="changed during restore/build"):
        package_plane.require_inventory_unchanged(before, after)


def test_locked_external_package_is_rehashed_at_final_inventory(tmp_path: Path) -> None:
    package = tmp_path / "Package.1.0.0.nupkg"
    write_package(package, b"original")
    locked = {package.name: hashlib.sha256(package.read_bytes()).hexdigest()}
    write_package(package, b"substituted")
    with pytest.raises(package_plane.VerificationError, match="locked package changed"):
        package_plane.package_inventory(tmp_path, {package.name}, locked)


def test_nested_or_linked_feed_entries_are_rejected(tmp_path: Path) -> None:
    package = tmp_path / "Package.1.0.0.nupkg"
    write_package(package, b"package")
    nested = tmp_path / "nested"
    nested.mkdir()
    with pytest.raises(package_plane.VerificationError, match="directory, link, or special"):
        package_plane.package_inventory(tmp_path, {package.name})
    nested.rmdir()
    target = tmp_path.parent / f"{tmp_path.name}-outside"
    target.mkdir()
    nested.symlink_to(target, target_is_directory=True)
    with pytest.raises(package_plane.VerificationError, match="directory, link, or special"):
        package_plane.package_inventory(tmp_path, {package.name})


def test_unexpected_feed_package_is_rejected(tmp_path: Path) -> None:
    write_package(tmp_path / "Expected.1.0.0.nupkg", b"expected")
    write_package(tmp_path / "Ambient.9.9.9.nupkg", b"ambient")
    with pytest.raises(package_plane.VerificationError, match="missing or unexpected"):
        package_plane.package_inventory(tmp_path, {"Expected.1.0.0.nupkg"})


def test_windows_runtime_closure_rows_sizes_authority_and_counts_are_exact() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    external = lock["externalPackages"]
    expected_rows = [
        {key: value for key, value in row.items() if key != "sizeBytes"}
        for row in package_plane.EXPECTED_WINDOWS_RUNTIME_PACKAGES
    ]
    locked_by_name = {row["fileName"]: row for row in external}

    assert [locked_by_name[row["fileName"]] for row in expected_rows] == expected_rows
    assert [row["sizeBytes"] for row in package_plane.EXPECTED_WINDOWS_RUNTIME_PACKAGES] == [
        40074136,
        12795776,
        5781842,
    ]
    assert len(external) == 86
    assert (
        len(external)
        + len(lock["canonicalOwnerFeed"]["packages"])
        + len(lock["packages"])
        == 100
    )
    authority = hashlib.sha256(
        json.dumps(external, sort_keys=True, separators=(",", ":")).encode("utf-8")
    ).hexdigest()
    assert authority == "04358b9b2a81e7429f3e69b5ab9b849033eabe261d8392625016db483a482ce0"


def test_windows_runtime_download_requires_the_fixed_official_size(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    package = {
        key: value
        for key, value in package_plane.EXPECTED_WINDOWS_RUNTIME_PACKAGES[0].items()
        if key != "sizeBytes"
    }
    package["sha256"] = hashlib.sha256(b"x").hexdigest()
    monkeypatch.setattr(
        package_plane.urllib.request,
        "urlopen",
        lambda *_args, **_kwargs: io.BytesIO(b"x"),
    )

    with pytest.raises(package_plane.VerificationError, match="fixed size differs"):
        package_plane.acquire_external_package(package, tmp_path)
    assert not (tmp_path / package["fileName"]).exists()


def test_retained_bundle_cli_and_path_safety(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    target = tmp_path / "windows-bundle"
    receipt = tmp_path / "receipt.json"
    monkeypatch.setattr(
        package_plane.sys,
        "argv",
        [
            str(SCRIPT),
            "--receipt-output",
            str(receipt),
            "--retain-windows-bundle-output",
            str(target),
        ],
    )
    assert package_plane.parse_args().retain_windows_bundle_output == target

    with pytest.raises(package_plane.VerificationError, match="absolute"):
        package_plane.validate_retained_bundle_target(Path("relative-output"))

    target.mkdir()
    with pytest.raises(package_plane.VerificationError, match="must be absent"):
        package_plane.validate_retained_bundle_target(target)
    target.rmdir()

    dangling = tmp_path / "dangling-output"
    dangling.symlink_to(tmp_path / "missing")
    with pytest.raises(package_plane.VerificationError, match="must be absent"):
        package_plane.validate_retained_bundle_target(dangling)

    linked_parent = tmp_path / "linked-parent"
    linked_parent.symlink_to(tmp_path, target_is_directory=True)
    with pytest.raises(package_plane.VerificationError, match="physical"):
        package_plane.validate_retained_bundle_target(linked_parent / "output")

    writable_parent = tmp_path / "writable-parent"
    writable_parent.mkdir()
    writable_parent.chmod(0o777)
    with pytest.raises(package_plane.VerificationError, match="group/world-writable"):
        package_plane.validate_retained_bundle_target(writable_parent / "output")
    writable_parent.chmod(0o700)

    staging = tmp_path / "stage"
    staging.mkdir()
    device = staging.stat().st_dev
    with pytest.raises(package_plane.VerificationError, match="cross-filesystem"):
        package_plane.require_same_filesystem(device + 1, staging)


def _retained_bundle_inputs(tmp_path: Path) -> dict[str, object]:
    feed = tmp_path / "feed"
    feed.mkdir()
    package = feed / "Package.1.0.0.nupkg"
    write_package(package, b"locked")
    locked = {package.name: hashlib.sha256(package.read_bytes()).hexdigest()}
    before = package_plane.package_inventory(feed, {package.name}, locked)
    config = tmp_path / "NuGet.Config"
    config.write_text("<configuration />\n", encoding="utf-8")
    lock_path = tmp_path / "package-plane.lock.json"
    lock_path.write_text("{}\n", encoding="utf-8")
    lock_inventory = package_plane.secure_regular_file_inventory(
        lock_path,
        label="test consumer lock",
        receipt_path=package_plane.CANONICAL_PACKAGE_PLANE_LOCK.as_posix(),
    )
    consumer = tmp_path / "consumer"
    project = consumer / package_plane.WINDOWS_PUBLISH_PROJECT
    project.parent.mkdir(parents=True)
    project.write_text("<Project />\n", encoding="utf-8")
    return {
        "consumer": consumer,
        "consumer_commit": "a" * 40,
        "consumer_config": config,
        "consumer_lock_inventory": lock_inventory,
        "environment": {"PATH": f"{tmp_path}/trusted-dotnet:/usr/bin:/bin"},
        "expected_feed_inventory": before,
        "expected_names": {package.name},
        "feed": feed,
        "locked_package_sha256": locked,
    }


def _write_complete_windows_publish(output: Path) -> dict[str, bytes]:
    assets = {
        "Chummer.Avalonia.deps.json": b"deps",
        "Chummer.Avalonia.dll": b"managed",
        "Chummer.Avalonia.exe": b"native-host",
        "Chummer.Avalonia.runtimeconfig.json": b"runtime",
        "exact-same-run-byte.dat": b"do-not-repack",
    }
    for name, content in assets.items():
        (output / name).write_bytes(content)
    return assets


def test_windows_publish_closure_is_atomically_retained_with_exact_same_run_bytes(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    inputs = _retained_bundle_inputs(tmp_path)
    target = tmp_path / "retained-&-quote-\"'-less-<"
    captured: dict[str, object] = {}

    def fake_run(
        command: list[str],
        *,
        cwd: Path,
        environment: dict[str, str],
        capture: bool = False,
    ) -> subprocess.CompletedProcess[str]:
        captured["command"] = command
        assert cwd == inputs["consumer"]
        assert environment == inputs["environment"]
        assert capture is False
        output = Path(command[command.index("--output") + 1])
        captured["assets"] = _write_complete_windows_publish(output)
        return subprocess.CompletedProcess(command, 0)

    monkeypatch.setattr(package_plane, "run", fake_run)
    monkeypatch.setattr(
        package_plane,
        "require_clean_consumer_head",
        lambda *_args, **_kwargs: None,
    )
    receipt = package_plane.publish_and_retain_windows_bundle(
        target,
        **inputs,
    )

    command = captured["command"]
    assert isinstance(command, list)
    assert command[0:4] == [
        str(package_plane.TRUSTED_BASH),
        "scripts/ai/with-package-plane.sh",
        "publish",
        package_plane.WINDOWS_PUBLISH_PROJECT,
    ]
    assert command[command.index("-f") + 1] == "net10.0"
    assert command[command.index("-r") + 1] == "win-x64"
    assert command[command.index("--self-contained") + 1] == "true"
    assert receipt["atomicallyRetained"] is True
    assert receipt["authority"] is False
    assert receipt["consumerCommit"] == "a" * 40
    assert receipt["targetPath"] == str(target)
    assert receipt["manifestIsAuthoritative"] is True
    manifest = json.loads((target / "manifest.json").read_text(encoding="utf-8"))
    assert manifest["feedInventory"]["beforePublishSha256"] == manifest["feedInventory"]["afterPublishSha256"]
    assert manifest["feedInventory"]["afterPublishSha256"] == manifest["feedInventory"]["retainedSha256"]
    assert manifest["assetInventory"]["afterPublishSha256"] == manifest["assetInventory"]["retainedSha256"]
    assert manifest["publish"]["status"] == "passed"
    assert manifest["publish"]["shell"] is False
    assert manifest["releaseEligibility"]["eligible"] is False
    assert manifest["deterministicRepacking"] is False
    assert manifest["retainedNugetConfig"]["usableAtRetainedTarget"] is True
    assert manifest["retainedNugetConfig"]["packageSource"] == str(target / "feed")
    package_plane.require_exact_nuget_config_source(
        target / "config" / "NuGet.Config",
        target / "feed",
    )
    assets = captured["assets"]
    assert isinstance(assets, dict)
    assert (target / "assets" / "exact-same-run-byte.dat").read_bytes() == assets["exact-same-run-byte.dat"]
    assert stat.S_IMODE((target / "assets" / "exact-same-run-byte.dat").stat().st_mode) == 0o600
    assert stat.S_IMODE((target / "feed" / "Package.1.0.0.nupkg").stat().st_mode) == 0o600
    assert not list(tmp_path.glob(".chummer-win-retain-*"))
    assert not list(tmp_path.glob("chummer-win-publish-*"))


@pytest.mark.parametrize(
    "failure",
    [
        "publish",
        "partial",
        "feed-tamper",
        "asset-link",
        "asset-hardlink",
        "empty-directory",
        "unreadable-directory",
        "windows-invalid",
        "windows-reserved",
        "windows-casefold",
    ],
)
def test_windows_publish_closure_failures_leave_no_target_or_staging(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    failure: str,
) -> None:
    inputs = _retained_bundle_inputs(tmp_path)
    target = tmp_path / "retained-windows"

    def fake_run(
        command: list[str],
        *,
        cwd: Path,
        environment: dict[str, str],
        capture: bool = False,
    ) -> subprocess.CompletedProcess[str]:
        output = Path(command[command.index("--output") + 1])
        if failure == "publish":
            raise package_plane.VerificationError("injected publish failure")
        if failure == "partial":
            (output / "Chummer.Avalonia.exe").write_bytes(b"partial")
        else:
            _write_complete_windows_publish(output)
        if failure == "feed-tamper":
            feed = inputs["feed"]
            assert isinstance(feed, Path)
            write_package(feed / "Package.1.0.0.nupkg", b"tampered")
        if failure == "asset-link":
            (output / "linked-asset").symlink_to(output / "Chummer.Avalonia.dll")
        if failure == "asset-hardlink":
            os.link(output / "Chummer.Avalonia.dll", output / "hardlinked-asset")
        if failure == "empty-directory":
            (output / "empty").mkdir()
        if failure == "unreadable-directory":
            unreadable = output / "unreadable"
            unreadable.mkdir()
            (unreadable / "secret.dll").write_bytes(b"secret")
            unreadable.chmod(0o000)
        if failure == "windows-invalid":
            (output / "bad:name.dll").write_bytes(b"invalid")
        if failure == "windows-reserved":
            (output / "CON.txt").write_bytes(b"reserved")
        if failure == "windows-casefold":
            (output / "Case.dll").write_bytes(b"one")
            (output / "case.DLL").write_bytes(b"two")
        return subprocess.CompletedProcess(command, 0)

    monkeypatch.setattr(package_plane, "run", fake_run)
    monkeypatch.setattr(
        package_plane,
        "require_clean_consumer_head",
        lambda *_args, **_kwargs: None,
    )
    with pytest.raises(package_plane.VerificationError):
        package_plane.publish_and_retain_windows_bundle(target, **inputs)
    assert not target.exists()
    assert not target.is_symlink()
    assert not list(tmp_path.glob(".chummer-win-retain-*"))
    assert not list(tmp_path.glob("chummer-win-publish-*"))


def test_atomic_retention_never_replaces_an_existing_target(tmp_path: Path) -> None:
    staging = tmp_path / "staging"
    target = tmp_path / "target"
    staging.mkdir()
    target.mkdir()
    marker = target / "owned"
    marker.write_text("preserve\n", encoding="utf-8")

    with pytest.raises(package_plane.VerificationError, match="appeared"):
        package_plane.atomic_rename_noreplace(staging, target)
    assert staging.is_dir()
    assert marker.read_text(encoding="utf-8") == "preserve\n"


def test_owned_staging_cleanup_does_not_mutate_an_external_hardlink_inode(
    tmp_path: Path,
) -> None:
    external = tmp_path / "external.bin"
    external.write_bytes(b"external-authority")
    external.chmod(0o644)
    original = external.lstat()
    staging = tmp_path / "owned-staging"
    staging.mkdir(mode=0o700)
    staging_metadata = staging.lstat()
    os.link(external, staging / "linked.bin")
    assert external.lstat().st_nlink == 2

    package_plane.remove_owned_staging_tree(
        staging,
        (staging_metadata.st_dev, staging_metadata.st_ino),
    )

    final = external.lstat()
    assert external.read_bytes() == b"external-authority"
    assert stat.S_IMODE(final.st_mode) == stat.S_IMODE(original.st_mode) == 0o644
    assert (final.st_dev, final.st_ino) == (original.st_dev, original.st_ino)
    assert final.st_nlink == original.st_nlink == 1
    assert not staging.exists()


def test_outer_receipt_failure_rolls_back_the_exact_retained_target(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    target = tmp_path / "retained"
    target.mkdir()
    (target / "manifest.json").write_text("{}\n", encoding="utf-8")
    metadata = target.lstat()
    args = package_plane.argparse.Namespace(
        receipt_output=tmp_path / "receipt.json",
        retain_windows_bundle_output=target,
        _retained_bundle_identity=(metadata.st_dev, metadata.st_ino),
    )
    monkeypatch.setattr(
        package_plane,
        "exact_write_receipt",
        lambda *_args, **_kwargs: (_ for _ in ()).throw(OSError("injected fsync failure")),
    )

    with pytest.raises(OSError, match="injected fsync failure"):
        package_plane.commit_verification_receipt(args, {"status": "passed"})
    assert not target.exists()
    assert args._retained_bundle_identity is None
    assert not list(tmp_path.glob(".chummer-win-rollback-*"))


def test_main_rolls_back_retention_and_owned_temporary_on_context_exit_failure(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    target = tmp_path / "retained"
    target.mkdir()
    (target / "manifest.json").write_text("{}\n", encoding="utf-8")
    target_metadata = target.lstat()
    temporary = tmp_path / "chummer-ui-fresh-package-plane-injected"
    temporary.mkdir()
    unreadable = temporary / "unreadable"
    unreadable.mkdir()
    (unreadable / "secret").write_bytes(b"secret")
    unreadable.chmod(0o000)
    temporary_metadata = temporary.lstat()
    args = package_plane.argparse.Namespace(
        current_owner_contract_feed=None,
        lock=LOCK,
        receipt_output=tmp_path / "receipt.json",
        repo_root=REPO_ROOT,
        retain_windows_bundle_output=target,
    )

    def fail_during_context_exit(namespace: object) -> dict[str, object]:
        setattr(
            namespace,
            "_retained_bundle_identity",
            (target_metadata.st_dev, target_metadata.st_ino),
        )
        setattr(namespace, "_verification_temporary_path", temporary)
        setattr(
            namespace,
            "_verification_temporary_identity",
            (temporary_metadata.st_dev, temporary_metadata.st_ino),
        )
        raise OSError("injected TemporaryDirectory cleanup failure")

    monkeypatch.setattr(package_plane, "parse_args", lambda: args)
    monkeypatch.setattr(package_plane, "verify", fail_during_context_exit)

    assert package_plane.main() == 2
    assert not target.exists()
    assert not temporary.exists()
    assert not args.receipt_output.exists()


def _git(command: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [str(package_plane.TRUSTED_GIT), *command],
        cwd=cwd,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=True,
    )


def test_consumer_head_capture_survives_branch_advance_and_rejects_lock_swap(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    source = tmp_path / "source"
    source.mkdir()
    _git(["init", "--quiet"], source)
    _git(["config", "user.email", "test@example.invalid"], source)
    _git(["config", "user.name", "Test"], source)
    lock = source / package_plane.CANONICAL_PACKAGE_PLANE_LOCK
    lock.parent.mkdir()
    lock.write_text('{"authority":"captured"}\n', encoding="utf-8")
    marker = source / "marker.txt"
    marker.write_text("one\n", encoding="utf-8")
    _git(["add", package_plane.CANONICAL_PACKAGE_PLANE_LOCK.as_posix(), marker.name], source)
    _git(["commit", "--quiet", "-m", "captured"], source)

    head, canonical, lock_bytes, captured_inventory = (
        package_plane.capture_consumer_authority(source, lock)
    )
    alternate = source / "alternate-lock.json"
    alternate.write_text("{}\n", encoding="utf-8")
    with pytest.raises(package_plane.VerificationError, match="canonical in-repo"):
        package_plane.capture_consumer_authority(source, alternate)
    alternate.unlink()
    assert canonical == lock

    marker.write_text("two\n", encoding="utf-8")
    _git(["add", marker.name], source)
    _git(["commit", "--quiet", "-m", "advanced"], source)
    assert _git(["rev-parse", "HEAD"], source).stdout.strip() != head

    consumer_parent = tmp_path / "consumers"
    consumer_parent.mkdir()
    consumer = consumer_parent / "exact"
    cloned_inventory = package_plane.clone_exact_consumer(
        source,
        consumer,
        consumer_parent,
        os.environ.copy(),
        head,
        lock_bytes,
    )
    assert cloned_inventory == captured_inventory
    assert _git(["rev-parse", "HEAD"], consumer).stdout.strip() == head

    swapped_consumer = consumer_parent / "swapped"

    def swap_lock(clone: Path, *_args: object, **_kwargs: object) -> None:
        (clone / package_plane.CANONICAL_PACKAGE_PLANE_LOCK).write_text(
            '{"authority":"swapped"}\n',
            encoding="utf-8",
        )

    monkeypatch.setattr(package_plane, "require_clean_consumer_head", swap_lock)
    with pytest.raises(package_plane.VerificationError, match="lock bytes differ"):
        package_plane.clone_exact_consumer(
            source,
            swapped_consumer,
            consumer_parent,
            os.environ.copy(),
            head,
            lock_bytes,
        )
