from __future__ import annotations

import importlib.util
import json
import zipfile
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
    rows = package_plane.verify_source_files(REPO_ROOT, lock["consumer"]["sourceFiles"])
    assert len(rows) == len(lock["consumer"]["sourceFiles"])


def test_forged_owner_pin_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["owners"][0]["commit"] = "main"
    with pytest.raises(package_plane.VerificationError, match="owner commit is not exact"):
        package_plane.validate_lock(lock)


def test_mutable_external_package_source_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["externalPackages"][0]["source"] = "https://api.nuget.org/v3/index.json"
    with pytest.raises(package_plane.VerificationError, match="immutable NuGet path"):
        package_plane.validate_lock(lock)


def test_missing_core_runtime_package_is_rejected() -> None:
    lock = json.loads(LOCK.read_text(encoding="utf-8"))
    lock["packages"][-1]["packageId"] = "Chummer.Rulesets.Sr7"
    with pytest.raises(package_plane.VerificationError, match="required Core runtime package"):
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


def test_unexpected_feed_package_is_rejected(tmp_path: Path) -> None:
    write_package(tmp_path / "Expected.1.0.0.nupkg", b"expected")
    write_package(tmp_path / "Ambient.9.9.9.nupkg", b"ambient")
    with pytest.raises(package_plane.VerificationError, match="missing or unexpected"):
        package_plane.package_inventory(tmp_path, {"Expected.1.0.0.nupkg"})
