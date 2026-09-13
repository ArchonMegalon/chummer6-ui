"""Real Git-index portability checks; no builds or external source access."""
import importlib.util
import os
from pathlib import Path
import subprocess
from types import SimpleNamespace

import pytest


SCRIPT = Path(__file__).resolve().parents[1] / "scripts/ai/verify_pull_request_controls.py"


def git(root, *args, input=None):
    return subprocess.run(["git", *args], cwd=root, input=input, check=True, capture_output=True,
                          timeout=15, env={"PATH": os.defpath, "GIT_CONFIG_NOSYSTEM": "1",
                                           "GIT_CONFIG_GLOBAL": os.devnull}).stdout


@pytest.fixture
def case(tmp_path):
    root = tmp_path / "repo"
    root.mkdir(mode=0o700)
    git(root, "init", "--quiet", "--template=")
    (root / "source").mkdir()
    (root / "source/data.txt").write_text("tracked source")
    git(root, "add", ".")
    spec = importlib.util.spec_from_file_location("source_link_controls", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.REPO_ROOT = root
    return SimpleNamespace(root=root, controls=module)


def link(case, name, target):
    # Write actual mode120000 index entries independently of filesystem support.
    oid = git(case.root, "hash-object", "-w", "--stdin", input=target.encode()).decode().strip()
    git(case.root, "update-index", "--add", "--cacheinfo", "120000", oid, name)


@pytest.mark.parametrize("target", ["source/data.txt", "./source/data.txt", "source/../source/data.txt", "source", "."])
def test_contained_links_are_accepted_without_reading_checkout_targets(case, monkeypatch, target):
    link(case, "ordinary-alias", target)
    def forbidden(*args, **kwargs):
        pytest.fail("index guard followed a filesystem target")
    monkeypatch.setattr(Path, "resolve", forbidden)
    monkeypatch.setattr(Path, "read_bytes", forbidden)
    monkeypatch.setattr(Path, "read_text", forbidden)
    case.controls.check_source_link_portability()


@pytest.mark.parametrize("target", ["/docker/host/source", "../outside", "source/../../outside", "source/../../../source/data.txt",
                                    "C:/host/source", "C:source", "\\\\server\\share", "missing", "source/data.txt/.."])
def test_nonportable_links_fail_without_exposing_target_content(case, target):
    link(case, "arbitrary-link-name", target)
    with pytest.raises(case.controls.ControlError) as error:
        case.controls.check_source_link_portability()
    assert "arbitrary-link-name" in str(error.value)
    assert target not in str(error.value)


@pytest.mark.parametrize("symlinks", ["true", "false"])
def test_index_mode_is_authoritative_for_real_and_emulated_symlink_checkout(case, symlinks):
    link(case, "alias", "../external")
    git(case.root, "config", "core.symlinks", symlinks)
    git(case.root, "checkout-index", "--all", "--force")
    assert (case.root / "alias").is_symlink() == (symlinks == "true")
    if symlinks == "false":
        assert (case.root / "alias").read_text() == "../external"
    with pytest.raises(case.controls.ControlError, match="escapes"):
        case.controls.check_source_link_portability()


def test_contained_link_chains_and_parent_hops_are_resolved_in_order(case):
    link(case, "source/near", "data.txt")
    link(case, "outer", "source/near")
    link(case, "directory", "source")
    link(case, "parent-hop", "directory/../source/near")
    case.controls.check_source_link_portability()


@pytest.mark.parametrize("target", ["a", "b", "source/up/../source/data.txt"])
def test_cycles_and_chain_escapes_are_rejected(case, target):
    link(case, "a", target)
    link(case, "b", "a")
    link(case, "source/up", "..")
    with pytest.raises(case.controls.ControlError):
        case.controls.check_source_link_portability()


def test_staged_deletion_removes_bad_link_from_inventory(case):
    link(case, "removed-alias", "/host/private-target")
    with pytest.raises(case.controls.ControlError):
        case.controls.check_source_link_portability()
    git(case.root, "update-index", "--force-remove", "removed-alias")
    case.controls.check_source_link_portability()


def test_main_checks_all_index_links_even_with_empty_changed_file_list(case, monkeypatch, capsys):
    link(case, "preexisting-link", "/host/private-target")
    monkeypatch.setattr(case.controls, "parse_args", lambda: SimpleNamespace(base=None, head=None))
    monkeypatch.setattr(case.controls, "changed_paths", lambda *args: [])
    assert case.controls.main() == 2
    assert "preexisting-link" in capsys.readouterr().err


def test_real_git_failures_are_reported_as_control_errors(case, monkeypatch):
    def failure(*args, **kwargs):
        raise subprocess.TimeoutExpired(["private-target"], 30)
    monkeypatch.setattr(subprocess, "run", failure)
    with pytest.raises(case.controls.ControlError, match="tracked source link inventory failed"):
        case.controls.check_source_link_portability()


@pytest.mark.parametrize("target", ["", "source/data.txt\0", "a" * 4097])
def test_invalid_or_oversized_link_blobs_fail_before_resolution(case, target):
    link(case, "malformed-link", target)
    with pytest.raises(case.controls.ControlError):
        case.controls.check_source_link_portability()


def test_git_observations_are_local_index_and_raw_object_reads(case, monkeypatch):
    link(case, "alias", "source/data.txt")
    original, calls = subprocess.run, []
    def observe(command, **kwargs):
        calls.append((command, kwargs))
        return original(command, **kwargs)
    monkeypatch.setattr(subprocess, "run", observe)
    case.controls.check_source_link_portability()
    assert [command[1:3] for command, _ in calls] == [
        ["ls-files", "--stage"], ["cat-file", "-s"], ["cat-file", "blob"]]
    assert all(kwargs["timeout"] == 30 and kwargs["env"]["GIT_NO_REPLACE_OBJECTS"] == "1"
               for _, kwargs in calls)
    assert all(kwargs["env"]["GIT_NO_LAZY_FETCH"] == "1" and kwargs["env"]["GIT_ALLOW_PROTOCOL"] == ""
               for _, kwargs in calls)


def test_missing_promisor_blob_cannot_start_a_locally_configured_remote_helper(case, tmp_path, monkeypatch):
    link(case, "alias", "source/data.txt")
    oid = git(case.root, "ls-files", "--stage", "alias").decode().split()[1]
    # Only this fixture's exact loose object is removed to model a partial clone.
    (case.root / ".git/objects" / oid[:2] / oid[2:]).unlink()
    bin_dir = tmp_path / "bin"
    bin_dir.mkdir()
    sentinel = tmp_path / "helper-ran"
    helper = bin_dir / "git-remote-portability-test"
    helper.write_text("#!/bin/sh\nprintf invoked > '" + str(sentinel) + "'\nexit 1\n")
    helper.chmod(0o700)
    monkeypatch.setattr(os, "defpath", str(bin_dir) + os.pathsep + os.defpath)
    git(case.root, "config", "remote.origin.url", "portability-test::offline-fixture")
    git(case.root, "config", "remote.origin.promisor", "true")
    git(case.root, "config", "protocol.portability-test.allow", "always")
    baseline = subprocess.run(["git", "cat-file", "-s", oid], cwd=case.root, capture_output=True, timeout=15,
                              env={"PATH": os.defpath, "GIT_CONFIG_NOSYSTEM": "1", "GIT_CONFIG_GLOBAL": os.devnull})
    assert baseline.returncode != 0 and sentinel.read_text() == "invoked"
    sentinel.unlink()
    with pytest.raises(case.controls.ControlError, match="inventory failed"):
        case.controls.check_source_link_portability()
    assert not sentinel.exists()
