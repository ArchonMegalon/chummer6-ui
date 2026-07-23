from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import shutil
import sys
import types
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]


def load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


fixtures = load(
    "unsigned_export_fixture_for_direct_import",
    ROOT / "tests" / "test_preview_nightly_unsigned_candidate_export.py",
)
coordinator = load(
    "preview_nightly_unsigned_direct_import_for_tests",
    ROOT / "scripts" / "run-preview-nightly-unsigned-direct-import.py",
)


def test_upload_identity_binds_exact_reconstructed_shelf(tmp_path: Path) -> None:
    values = fixtures.fixture(tmp_path)
    fixtures.exporter.export_candidate(values["args"])
    bundle = tmp_path / "bundle"
    fixtures.exporter.reconstruct_publication(
        values["output"],
        values["source"]["incumbent"],
        bundle,
        values["args"].expected_version,
        values["args"].expected_manifest_sha256,
        values["args"].source_sha,
    )
    stage = tmp_path / "stage"
    stage.mkdir()
    coordinator.materialize_upload_identity(
        bundle, stage, values["args"].expected_version
    )
    inventory = json.loads(
        (stage / coordinator.CANDIDATE_INVENTORY_NAME).read_text()
    )
    summary = json.loads((stage / coordinator.CANDIDATE_SUMMARY_NAME).read_text())
    assert inventory["contractName"] == (
        "chummer.release-upload.candidate-inventory/v1"
    )
    assert inventory["files"] == sorted(
        inventory["files"], key=lambda row: row["path"]
    )
    assert summary["inventorySha256"] == coordinator.inventory_digest(
        inventory["files"]
    )
    assert summary["canonicalManifestSha256"] == hashlib.sha256(
        (bundle / coordinator.CANONICAL_NAME).read_bytes()
    ).hexdigest()
    identity = {
        key: summary[key]
        for key in (
            "version",
            "canonicalManifestSha256",
            "inventorySha256",
            "fileCount",
            "totalBytes",
        )
    }
    assert summary["bundleIdentitySha256"] == hashlib.sha256(
        json.dumps(identity, sort_keys=True, separators=(",", ":")).encode()
    ).hexdigest()


def test_repository_authority_requires_clean_exact_origin_main(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    root = tmp_path.resolve()
    commit = "a" * 40
    values = {
        ("rev-parse", "--show-toplevel"): str(root),
        ("remote", "get-url", "origin"): coordinator.UI_ORIGIN,
        ("rev-parse", "HEAD"): commit,
        ("rev-parse", "refs/remotes/origin/main^{commit}"): commit,
        ("status", "--porcelain", "--untracked-files=normal"): "",
    }
    monkeypatch.setattr(
        coordinator, "git_value", lambda _root, *args: values[args]
    )
    assert coordinator.verify_repository(
        root, commit, coordinator.UI_ORIGIN, "UI"
    ) == root
    values[("status", "--porcelain", "--untracked-files=normal")] = " M file"
    with pytest.raises(coordinator.ImportError, match="not clean"):
        coordinator.verify_repository(root, commit, coordinator.UI_ORIGIN, "UI")


def test_coordinator_freezes_additive_v2_v3_graph_without_linux_bridge() -> None:
    source = (
        ROOT / "scripts" / "run-preview-nightly-unsigned-direct-import.py"
    ).read_text(encoding="utf-8")
    for required in (
        '"prepare"',
        '"finalize"',
        '"--composition-request"',
        '"--expected-composition-request-sha256"',
        '"--registry-source-sha"',
        '"--expected-unsigned-scope-sha256"',
        '"--registry-candidate-receipt"',
        '"--registry-finalize-authority"',
        '"--registry-finalize-receipt"',
        '"sealed_review_required"',
    ):
        assert required in source
    assert '"--evidence-root"' not in source
    assert '"--windows-finalized-root"' not in source
    assert "materialize_preview_publication_delta.py" not in source
    assert "subprocess.run(" in source
    assert "shell=True" not in source


def test_coordinator_cli_requires_all_three_exact_source_authorities() -> None:
    parser = coordinator.parse_args
    with pytest.raises(SystemExit):
        parser([])
    args = parser(
        [
            "--export-root",
            "/tmp/export",
            "--incumbent-root",
            "/tmp/incumbent",
            "--registry-repo-root",
            "/tmp/registry",
            "--registry-source-sha",
            "b" * 40,
            "--hub-repo-root",
            "/tmp/hub",
            "--hub-source-sha",
            "c" * 40,
            "--expected-version",
            "run-20260722-150000",
            "--expected-manifest-sha256",
            "d" * 64,
            "--ui-source-sha",
            "a" * 40,
            "--output-root",
            "/tmp/output",
        ]
    )
    assert args.ui_source_sha == "a" * 40
    assert args.registry_source_sha == "b" * 40
    assert args.hub_source_sha == "c" * 40


def test_final_private_tree_rejects_links_and_case_collisions(tmp_path: Path) -> None:
    (tmp_path / "one").write_text("one")
    os.symlink("one", tmp_path / "two")
    with pytest.raises(coordinator.ImportError, match="symbolic link"):
        coordinator.validate_private_tree(tmp_path)
    (tmp_path / "two").unlink()
    (tmp_path / "Case").write_text("upper")
    (tmp_path / "case").write_text("lower")
    with pytest.raises(coordinator.ImportError, match="case-collides"):
        coordinator.validate_private_tree(tmp_path)


def test_v3_profile_rejects_raw_or_partially_projected_prepare_pair(
    tmp_path: Path,
) -> None:
    registry_sha = "b" * 40
    source = tmp_path / "source"
    projected = tmp_path / "projected"
    source.mkdir()
    projected.mkdir()
    for name in (coordinator.CANONICAL_NAME, coordinator.COMPATIBILITY_NAME):
        (source / name).write_text('{"source":true}\n')
        (projected / name).write_text('{"source":true}\n')
    with pytest.raises(coordinator.ImportError, match="unprojected UI-source"):
        coordinator.require_projected_manifest_pair(
            coordinator.COMPOSITION.PROJECTION_PROFILE,
            registry_sha,
            source,
            projected,
        )

    projected_document = {
        "projectionProfile": coordinator.COMPOSITION.PROJECTION_PROFILE,
        "registryCommit": registry_sha,
        "registry_commit": registry_sha,
    }
    (projected / coordinator.CANONICAL_NAME).write_text(
        json.dumps(projected_document) + "\n"
    )
    with pytest.raises(coordinator.ImportError, match="releases.json"):
        coordinator.require_projected_manifest_pair(
            coordinator.COMPOSITION.PROJECTION_PROFILE,
            registry_sha,
            source,
            projected,
        )

    (projected / coordinator.COMPATIBILITY_NAME).write_text(
        json.dumps(projected_document) + "\n"
    )
    coordinator.require_projected_manifest_pair(
        coordinator.COMPOSITION.PROJECTION_PROFILE,
        registry_sha,
        source,
        projected,
    )
    with pytest.raises(coordinator.ImportError, match="profile differs"):
        coordinator.require_projected_manifest_pair(
            "legacy_byte_copy", registry_sha, source, projected
        )
    projected_document["registryCommit"] = "c" * 40
    (projected / coordinator.CANONICAL_NAME).write_text(
        json.dumps(projected_document) + "\n"
    )
    with pytest.raises(coordinator.ImportError, match="projection identity"):
        coordinator.require_projected_manifest_pair(
            coordinator.COMPOSITION.PROJECTION_PROFILE,
            registry_sha,
            source,
            projected,
        )


def test_child_commands_do_not_inherit_git_python_or_loader_poison(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv("GIT_DIR", "/poison/git")
    monkeypatch.setenv("GIT_WORK_TREE", "/poison/tree")
    monkeypatch.setenv("GIT_CONFIG_GLOBAL", "/poison/config")
    monkeypatch.setenv("PYTHONPATH", "/poison/python")
    monkeypatch.setenv("PYTHONHOME", "/poison/home")
    monkeypatch.setenv("LD_PRELOAD", "/poison/loader.so")
    observed: dict[str, object] = {}

    def fake_run(arguments: list[str], **kwargs: object):
        observed["arguments"] = arguments
        observed["environment"] = kwargs.get("env")
        return types.SimpleNamespace(returncode=0, stdout="ok\n", stderr="")

    monkeypatch.setattr(coordinator.subprocess, "run", fake_run)
    assert coordinator.run_checked(["git", "rev-parse", "HEAD"], label="test") == "ok"
    environment = observed["environment"]
    assert isinstance(environment, dict)
    assert set(environment).issubset(
        {"PATH", "HOME", "LANG", "XDG_RUNTIME_DIR", "LC_ALL", "LC_CTYPE"}
        | {key for key in environment if key.startswith("LC_")}
    )
    for poisoned in (
        "GIT_DIR",
        "GIT_WORK_TREE",
        "GIT_CONFIG_GLOBAL",
        "PYTHONPATH",
        "PYTHONHOME",
        "LD_PRELOAD",
    ):
        assert poisoned not in environment


def test_pipeline_uses_isolated_registry_transactions_then_removes_them(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    values = fixtures.fixture(tmp_path / "fixture")
    fixtures.exporter.export_candidate(values["args"])
    for path in values["output"].rglob("*"):
        path.chmod(0o755 if path.is_dir() else 0o644)
    registry_root = tmp_path / "registry"
    hub_root = tmp_path / "hub"
    registry_script = registry_root / coordinator.REGISTRY_SCRIPT
    hub_script = hub_root / coordinator.HUB_SCRIPT
    registry_script.parent.mkdir(parents=True)
    hub_script.parent.mkdir(parents=True)
    registry_script.write_text("# fixture\n")
    hub_script.write_text("# fixture\n")
    monkeypatch.setattr(
        coordinator,
        "verify_repository",
        lambda root, *_args: Path(root).resolve(strict=True),
    )
    commands: list[list[str]] = []

    def option(command: list[str], name: str) -> Path:
        return Path(command[command.index(name) + 1])

    def fake_run(
        command: list[str], *, label: str, cwd: Path | None = None
    ) -> str:
        del label, cwd
        commands.append(command)
        if len(command) > 2 and command[2] == "prepare":
            publication = option(command, "--publication-root")
            for flag, name in (
                ("--output-manifest", coordinator.CANONICAL_NAME),
                (
                    "--output-compatibility-manifest",
                    coordinator.COMPATIBILITY_NAME,
                ),
            ):
                target = option(command, flag)
                target.parent.mkdir(parents=True, exist_ok=True)
                projected = json.loads((publication / name).read_text())
                projected["registryPreparedProjection"] = True
                projected["projectionProfile"] = (
                    coordinator.COMPOSITION.PROJECTION_PROFILE
                )
                projected["registryCommit"] = command[
                    command.index("--registry-source-sha") + 1
                ]
                projected["registry_commit"] = projected["registryCommit"]
                for row in projected.get("artifacts", []):
                    file_name = row.get("fileName")
                    if file_name:
                        row["downloadUrl"] = f"/downloads/files/{file_name}"
                    payload_name = row.get("payloadFileName")
                    if payload_name:
                        row["payloadDownloadUrl"] = (
                            f"/downloads/files/{payload_name}"
                        )
                for row in projected.get("downloads", []):
                    file_name = row.get("fileName")
                    if file_name:
                        relative_url = f"/downloads/files/{file_name}"
                        row["url"] = relative_url
                        row["downloadUrl"] = relative_url
                    payload_name = row.get("payloadFileName")
                    if payload_name:
                        row["payloadDownloadUrl"] = (
                            f"/downloads/files/{payload_name}"
                        )
                target.write_text(
                    json.dumps(projected, indent=2, sort_keys=True) + "\n"
                )
            receipt = option(command, "--output-candidate-receipt")
            receipt.write_text("{}\n")
        elif len(command) > 2 and command[2] == "finalize":
            for flag in ("--output-authority", "--output-finalize-receipt"):
                target = option(command, flag)
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text("{}\n")
        else:
            output = option(command, "--output")
            output.write_text("{}\n")
        return ""

    monkeypatch.setattr(coordinator, "run_checked", fake_run)
    output = tmp_path / "sealed"
    args = argparse.Namespace(
        export_root=values["output"],
        incumbent_root=values["source"]["incumbent"],
        registry_repo_root=registry_root,
        registry_source_sha="b" * 40,
        hub_repo_root=hub_root,
        hub_source_sha="c" * 40,
        expected_version=values["args"].expected_version,
        expected_manifest_sha256=values["args"].expected_manifest_sha256,
        ui_source_sha=values["args"].source_sha,
        output_root=output,
    )
    receipt = coordinator.run_pipeline(args)
    assert receipt["status"] == "sealed_review_required"
    assert not (output / "registry-transactions").exists()
    assert all(
        (output / name).is_file()
        for name in (
            coordinator.CANONICAL_NAME,
            coordinator.COMPATIBILITY_NAME,
            coordinator.REGISTRY_CANDIDATE_NAME,
            coordinator.REGISTRY_AUTHORITY_NAME,
            coordinator.REGISTRY_FINALIZE_NAME,
        )
    )
    prepare = next(command for command in commands if command[2] == "prepare")
    prepare_outputs = [
        option(prepare, flag)
        for flag in (
            "--output-manifest",
            "--output-compatibility-manifest",
            "--output-candidate-receipt",
        )
    ]
    assert len({path.parent for path in prepare_outputs}) == 1
    assert prepare_outputs[0].parent.name == "prepare"
    assert option(prepare, "--registry-source-sha") == Path("b" * 40)
    finalize = next(command for command in commands if command[2] == "finalize")
    finalize_outputs = [
        option(finalize, flag)
        for flag in ("--output-authority", "--output-finalize-receipt")
    ]
    assert len({path.parent for path in finalize_outputs}) == 1
    assert finalize_outputs[0].parent.name == "finalize"
    assert option(finalize, "--registry-source-sha") == Path("b" * 40)
    assert option(finalize, "--candidate-manifest").parent.name == "prepare"
    projected_manifest = output / "bundle" / coordinator.CANONICAL_NAME
    projected_compatibility = output / "bundle" / coordinator.COMPATIBILITY_NAME
    assert json.loads(projected_manifest.read_text())[
        "registryPreparedProjection"
    ] is True
    assert json.loads(projected_compatibility.read_text())[
        "registryPreparedProjection"
    ] is True
    assert (output / coordinator.CANONICAL_NAME).read_bytes() == (
        projected_manifest.read_bytes()
    )
    assert "registryPreparedProjection" not in json.loads(
        (values["output"] / fixtures.exporter.MANIFEST_PATH).read_text()
    )
    composition = json.loads(
        (output / coordinator.COMPOSITION_NAME).read_text()
    )
    assert composition["proposedCanonicalManifest"]["sha256"] != hashlib.sha256(
        projected_manifest.read_bytes()
    ).hexdigest()
    projected_scope = json.loads((output / coordinator.SCOPE_NAME).read_text())
    inventory_by_path = {
        row["path"]: row for row in projected_scope["fullShelfInventory"]
    }
    assert inventory_by_path[coordinator.CANONICAL_NAME]["sha256"] == hashlib.sha256(
        projected_manifest.read_bytes()
    ).hexdigest()
    raw_canonical = output / coordinator.SOURCE_CANONICAL_PATH
    raw_compatibility = output / coordinator.SOURCE_COMPATIBILITY_PATH
    assert raw_canonical.read_bytes() == (
        values["output"] / fixtures.exporter.MANIFEST_PATH
    ).read_bytes()
    assert raw_compatibility.read_bytes() == (
        values["output"] / fixtures.exporter.COMPATIBILITY_PATH
    ).read_bytes()
    assert receipt["transport"]["sourceCanonicalManifest"] == (
        coordinator.byte_reference(
            raw_canonical, coordinator.SOURCE_CANONICAL_PATH
        )
    )
    assert receipt["transport"]["sourceCompatibilityManifest"] == (
        coordinator.byte_reference(
            raw_compatibility, coordinator.SOURCE_COMPATIBILITY_PATH
        )
    )
    assert composition["proposedCanonicalManifest"]["sha256"] == (
        receipt["transport"]["sourceCanonicalManifest"]["sha256"]
    )
    assert composition["proposedCompatibilityManifest"]["sha256"] == (
        receipt["transport"]["sourceCompatibilityManifest"]["sha256"]
    )
    bundle_paths = {
        row["path"]
        for row in json.loads(
            (output / coordinator.CANDIDATE_INVENTORY_NAME).read_text()
        )["files"]
    }
    assert coordinator.SOURCE_CANONICAL_PATH not in bundle_paths
    assert coordinator.SOURCE_COMPATIBILITY_PATH not in bundle_paths
