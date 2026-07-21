from __future__ import annotations

import importlib.util
import hashlib
import json
import os
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
    engine = next(
        row for row in lock["packages"] if row["packageId"] == "Chummer.Engine.Contracts"
    )
    engine["version"] = "5.225.0.0"
    expected_packages = dict(package_plane.EXPECTED_PACKAGES)
    expected_packages["Chummer.Engine.Contracts"] = (
        engine["ownerDirectory"],
        engine["project"],
        engine["fileName"],
        engine["version"],
    )
    monkeypatch.setattr(package_plane, "EXPECTED_PACKAGES", expected_packages)
    with pytest.raises(package_plane.VerificationError, match="canonical package identity"):
        package_plane.validate_lock(lock)


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
    parent = {
        "PATH": os.environ["PATH"],
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

    environment = package_plane.isolated_child_environment(tmp_path / "caches", parent)

    assert environment["HTTP_PROXY"] == parent["HTTP_PROXY"]
    assert environment["PATH"] == parent["PATH"]
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
    assert "-p:ChummerEngineContractsPackageVersion=5.225.0" in source
    assert "-p:ChummerLocalContractsProject=" in source
    assert "-p:ChummerUseLocalCompatibilityTree=false" in source
    assert "-p:RestoreLockedMode=false" not in source
    assert "-p:RestorePackagesWithLockFile=false" not in source
    assert source.count("-p:RestoreLockedMode=true") == 1
    assert "canonical_feed_receipt = import_hub_canonical_feed(" in source
    assert "if package[\"packageId\"] in HUB_CANONICAL_PACKAGE_IDS:" in source
    assert source.count("-warnaserror:NU1603,NU1608") == 2
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
        "5.225.0</ChummerContractsPackageVersion>"
    ) in props
    assert 'contracts_version="${CHUMMER_CONTRACTS_PACKAGE_VERSION:-5.225.0}"' in helper
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


def test_private_sdk_and_every_execution_are_bound_to_exact_program_version() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    assert "sdk_root, sdk_archive_sha512 = acquire_sdk(" in source
    assert "owner_sdk_versions" in source
    assert '"sdkArchiveSha512": sdk_archive_sha512' in source
    assert '"buildExecutions": build_executions' in source
    assert '"testExecutions": test_executions' in source
    assert '"contractVersion": 5' in source
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
