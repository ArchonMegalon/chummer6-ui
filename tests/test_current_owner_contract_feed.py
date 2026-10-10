from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import os
import subprocess
from pathlib import Path
from types import ModuleType

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "ai" / "verify_fresh_checkout_package_plane.py"
LOCK = REPO_ROOT / "config" / "package-plane.lock.json"


def load_module() -> ModuleType:
    spec = importlib.util.spec_from_file_location("fresh_package_plane_current", SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


package_plane = load_module()


def canonical_digest(value: object) -> str:
    encoded = json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def test_current_owner_contract_feed_is_separate_and_reproducible() -> None:
    lock = package_plane.load_json(LOCK)
    package_plane.validate_lock(lock)
    assert lock["contractVersion"] == 11

    current = lock["currentOwnerContractFeed"]
    canonical = lock["canonicalOwnerFeed"]
    assert current["producerCommit"] == "ed36cd2d2fab3344a44dbddc2f4b1866d362877d"
    assert current["lockContract"] == "chummer-core.package-plane-lock/v1"
    assert current["lockSha256"] == "ac731fe6e4ce7f9f2b7173fcec600769f0f76566734dc962f0ed61f68527e1fd"
    assert current["packageVersion"] == "0.0.0-packageplane.20260721.1"
    assert current["inventoryContract"] == "chummer-core.owner-contract-package-inventory/v1"
    assert current["inventorySha256"] == "81c92d4c8ce94a302fd094f6eb666bf32e338e5047b9c91b8fb37058192ab4d0"
    assert current["inventorySha256"] != current["packageFeedInventorySha256"]
    assert current["producerSha256"] == "5beffe15d1708f4cf2c096f376822cd48b9502b5fb1acb69b95db3c86c474786"

    feed_rows = sorted(
        (
            {
                "fileName": row["fileName"],
                "sha256": row["sha256"],
                "sizeBytes": row["sizeBytes"],
            }
            for row in current["packages"]
        ),
        key=lambda row: row["fileName"],
    )
    assert canonical_digest(feed_rows) == current["packageFeedInventorySha256"]
    assert current["packageFeedInventorySha256"] == "ad220c6384644fcd83135e70bb33913e546c758eedfa2fd6da514714730285ca"

    assert canonical["producerCommit"] == "c77395de9f733427ef952c851f4a95b063cb5573"
    assert canonical["lockContract"] == "chummer-hub.package-plane-lock/v5"
    assert canonical["inventoryContract"] == "chummer-hub.external-package-inventory/v4"
    assert len(canonical["packages"]) == 4
    assert canonical["packageVersion"] == "0.1.1-packageplane.20260927.1"
    assert current["lockContract"] != canonical["lockContract"]
    assert current["inventoryContract"] != canonical["inventoryContract"]
    assert current["packageVersion"] != canonical["packageVersion"]


@pytest.mark.parametrize(
    ("field", "value"),
    (
        ("producerCommit", "f" * 40),
        ("producerSha256", "f" * 64),
        ("inventorySha256", "f" * 64),
        ("packageFeedInventorySha256", "f" * 64),
    ),
)
def test_substituted_current_owner_contract_authority_is_rejected(
    field: str, value: str
) -> None:
    lock = package_plane.load_json(LOCK)
    forged = copy.deepcopy(lock)
    forged["currentOwnerContractFeed"][field] = value
    with pytest.raises(
        package_plane.VerificationError,
        match="current owner-contract package authority",
    ):
        package_plane.validate_lock(forged)


def test_current_feed_validation_is_distinct_from_full_feed_import() -> None:
    source = SCRIPT.read_text(encoding="utf-8")
    assert "def validate_materialized_current_owner_contract_feed(" in source
    assert "current_owner_contract_feed_binding_receipt(lock)" in source
    assert 'parser.add_argument("--current-owner-contract-feed", type=Path)' in source
    assert '"selectedForCanonicalFullFeed": False' in source
    assert '"currentOwnerContractFeed": (' in source
    assert "destination_feed" not in source.split(
        "def validate_materialized_current_owner_contract_feed(", 1
    )[1].split("def import_current_owner_contract_feed(", 1)[0]

    lock = package_plane.load_json(LOCK)
    receipt = package_plane.current_owner_contract_feed_binding_receipt(lock)
    assert receipt["status"] == "bound_not_selected"
    assert receipt["materializedFeedValidated"] is False
    assert receipt["selectedForCanonicalFullFeed"] is False


def make_cold_owner_feed(tmp_path: Path) -> tuple[dict, Path, Path]:
    lock = package_plane.load_json(LOCK)
    authority = lock["currentOwnerContractFeed"]
    feed = tmp_path / "reused-feed"
    feed.mkdir()
    core = tmp_path / "core"
    for field, content in (("producerPath", b"# exact test producer\n"),
                           ("lockPath", b"{\"fixture\": true}\n")):
        path = core / authority[field]
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)
        authority["producerSha256" if field == "producerPath" else "lockSha256"] = hashlib.sha256(content).hexdigest()
    for row in authority["packages"]:
        content = ("synthetic package " + row["packageId"]).encode()
        (feed / row["fileName"]).write_bytes(content)
        row["sha256"] = hashlib.sha256(content).hexdigest()
        row["sizeBytes"] = len(content)
    authority["packageFeedInventorySha256"] = canonical_digest(sorted(
        ({key: row[key] for key in ("fileName", "sha256", "sizeBytes")}
         for row in authority["packages"]), key=lambda row: row["fileName"]))
    inventory = package_plane.expected_current_owner_contract_inventory(lock)
    raw = json.dumps(inventory, sort_keys=True).encode()
    (feed / authority["inventoryFileName"]).write_bytes(raw)
    authority["inventorySha256"] = hashlib.sha256(raw).hexdigest()
    return lock, feed, core


def test_cold_owner_contracts_reuse_exact_bytes_without_fetch_or_build(tmp_path, monkeypatch):
    lock, source, core = make_cold_owner_feed(tmp_path)
    destination = tmp_path / "destination"
    destination.mkdir()
    commands = []
    monkeypatch.setattr(package_plane, "run", lambda command, **kwargs: commands.append(command))
    receipt = package_plane.import_current_owner_contract_feed(
        lock, core, tmp_path / "sdk", tmp_path / "materialized",
        tmp_path / "workspace", tmp_path / "packages", destination, {},
        prebuilt_owner_feed=source,
    )
    assert len(commands) == 1
    assert commands[0][-1] == "--validate-only"
    assert receipt["status"] == "passed"
    assert receipt["reusedExactContractArtifacts"] is True
    assert receipt["selectedForCanonicalFullFeed"] is True
    for row in lock["currentOwnerContractFeed"]["packages"]:
        assert (destination / row["fileName"]).read_bytes() == (source / row["fileName"]).read_bytes()


@pytest.mark.parametrize("damage", ["changed", "missing", "extra", "symlink", "inventory", "oversized"])
def test_cold_owner_contracts_reject_non_exact_feed_before_execution(tmp_path, monkeypatch, damage):
    lock, source, core = make_cold_owner_feed(tmp_path)
    authority = lock["currentOwnerContractFeed"]
    package = source / authority["packages"][0]["fileName"]
    if damage == "changed":
        package.write_bytes(b"X" * package.stat().st_size)
    elif damage == "missing":
        package.unlink()
    elif damage == "extra":
        (source / "extra.nupkg").write_bytes(b"not admitted")
    elif damage == "symlink":
        content = package.read_bytes()
        package.unlink()
        target = tmp_path / "alias-target"
        target.write_bytes(content)
        package.symlink_to(target)
    elif damage == "inventory":
        (source / authority["inventoryFileName"]).write_text("{}")
    else:
        (source / authority["inventoryFileName"]).write_bytes(b" " * 65537)
    def forbidden(*args, **kwargs):
        pytest.fail("invalid prebuilt contracts must not execute a producer")
    monkeypatch.setattr(package_plane, "run", forbidden)
    with pytest.raises(package_plane.VerificationError):
        package_plane.import_current_owner_contract_feed(
            lock, core, tmp_path / "sdk", tmp_path / "materialized",
            tmp_path / "workspace", tmp_path / "packages", tmp_path / "destination", {},
            prebuilt_owner_feed=source,
        )


def test_cold_owner_contracts_reject_source_changed_during_validation(tmp_path, monkeypatch):
    lock, source, core = make_cold_owner_feed(tmp_path)
    destination = tmp_path / "destination"
    destination.mkdir()
    def mutate_source(*args, **kwargs):
        row = lock["currentOwnerContractFeed"]["packages"][0]
        (source / row["fileName"]).write_bytes(b"X" * row["sizeBytes"])
    monkeypatch.setattr(package_plane, "run", mutate_source)
    with pytest.raises(package_plane.VerificationError):
        package_plane.import_current_owner_contract_feed(
            lock, core, tmp_path / "sdk", tmp_path / "materialized",
            tmp_path / "workspace", tmp_path / "packages", destination, {},
            prebuilt_owner_feed=source,
        )


def make_local_owner_source(tmp_path):
    cache = tmp_path / "source-cache"
    cache.mkdir()
    seed = cache / "seed"
    seed.mkdir()
    environment = dict(os.environ, GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL="/dev/null")
    def git(*args):
        return subprocess.run(["git", "-C", str(seed), *args], env=environment,
            text=True, check=True, capture_output=True).stdout.strip()
    git("init", "--quiet")
    (seed / "source.txt").write_text("synthetic committed source\n")
    git("add", "source.txt")
    git("-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid",
        "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "fixture")
    commit = git("rev-parse", "HEAD")
    repository = "https://github.com/example/exact-owner.git"
    git("remote", "add", "origin", repository)
    source = cache / commit
    seed.rename(source)
    return cache, source, {"directory": "owner", "repository": repository, "commit": commit}, environment


def test_local_owner_source_is_really_cloned_without_remote_fetch(tmp_path, monkeypatch):
    cache, source, owner, environment = make_local_owner_source(tmp_path)
    target = tmp_path / "owners"
    target.mkdir()
    original = package_plane.run
    fetches = []
    def inspect(command, **kwargs):
        if "fetch" in command:
            fetches.append(command)
            assert command[-2:] == [str(source), owner["commit"]]
        return original(command, **kwargs)
    monkeypatch.setattr(package_plane, "run", inspect)
    checkout = package_plane.acquire_owner(owner, target, environment, source_cache=cache)
    assert (checkout / "source.txt").read_text() == "synthetic committed source\n"
    assert len(fetches) == 1
    assert package_plane.owner_fetch_source(owner, cache, environment) == str(source)
    assert subprocess.check_output(["git", "-C", str(checkout), "remote", "get-url", "origin"],
                                  env=environment, text=True).strip() == owner["repository"]


@pytest.mark.parametrize("damage", ["dirty", "origin", "head", "alias", "alternates", "gitlink"])
def test_local_owner_source_rejects_substitution_without_remote_fallback(tmp_path, monkeypatch, damage):
    cache, source, owner, environment = make_local_owner_source(tmp_path)
    if damage == "dirty":
        (source / "source.txt").write_text("changed\n")
    elif damage == "origin":
        owner = dict(owner, repository="https://github.com/example/wrong.git")
    elif damage == "head":
        wrong = "f" * 40
        source.rename(cache / wrong)
        owner = dict(owner, commit=wrong)
    elif damage == "alias":
        alias = tmp_path / "alias"
        alias.symlink_to(cache, target_is_directory=True)
        cache = alias
    elif damage == "alternates":
        (source / ".git/objects/info/alternates").write_text("/not-an-authority\n")
    else:
        moved = tmp_path / "moved-git"
        (source / ".git").rename(moved)
        (source / ".git").symlink_to(moved, target_is_directory=True)
    original = package_plane.run
    def inspect(command, **kwargs):
        assert "fetch" not in command, "invalid cache must not fall back to network"
        return original(command, **kwargs)
    monkeypatch.setattr(package_plane, "run", inspect)
    target = tmp_path / "owners"
    target.mkdir()
    with pytest.raises(package_plane.VerificationError):
        package_plane.acquire_owner(owner, target, environment, source_cache=cache)
    assert not (target / "owner").exists()
