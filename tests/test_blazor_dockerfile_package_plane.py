from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DOCKERFILE = ROOT / "Chummer.Blazor" / "Dockerfile"
PACKAGE_PLANE_LOCK = ROOT / "config" / "package-plane.lock.json"
REQUIRED_PACKAGE_PLANE = json.loads(
    PACKAGE_PLANE_LOCK.read_text(encoding="utf-8")
)["currentOwnerContractFeed"]["packageVersion"]


def test_blazor_dockerfile_uses_one_current_owner_package_plane() -> None:
    source = DOCKERFILE.read_text(encoding="utf-8")
    package_plane_versions = set(
        re.findall(r"-p:PackageVersion=(0\.0\.0-packageplane\.[^\s\\]+)", source)
    )

    assert package_plane_versions == {REQUIRED_PACKAGE_PLANE}
    assert source.count(f"-p:PackageVersion={REQUIRED_PACKAGE_PLANE}") == 3
    assert source.count(f"-p:Version={REQUIRED_PACKAGE_PLANE}") == 3
    assert "-p:ChummerUseLocalCompatibilityTree=true" in source
    assert source.count("-p:ChummerUseLockedOwnerContractPackages=true") == 2
    assert source.count(
        f"-p:ChummerRunContractsPackageVersion={REQUIRED_PACKAGE_PLANE}"
    ) == 2
    assert source.count(
        f"-p:ChummerHubRegistryContractsPackageVersion={REQUIRED_PACKAGE_PLANE}"
    ) == 2
