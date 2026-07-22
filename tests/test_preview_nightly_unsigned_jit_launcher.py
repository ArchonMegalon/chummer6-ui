from __future__ import annotations

import importlib.util
import shutil
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


fixtures = load(
    "unsigned_export_fixture_for_jit",
    ROOT / "tests" / "test_preview_nightly_unsigned_candidate_export.py",
)
launcher = load(
    "preview_nightly_unsigned_jit_launcher_for_tests",
    ROOT / "scripts" / "preview_nightly_unsigned_jit_launcher.py",
)


def test_wrapper_selects_exact_unsigned_workflow_and_no_capture_lane() -> None:
    assert launcher.legacy.WORKFLOW_PATH == (
        ".github/workflows/unsigned-windows-preview-nightly-candidate-export.yml"
    )
    assert launcher.legacy.WORKFLOW_FILE == (
        "unsigned-windows-preview-nightly-candidate-export.yml"
    )
    assert launcher.legacy.EXPORT_JOB_NAME == (
        "Export exact unsigned Windows candidate bytes"
    )
    assert launcher.legacy.UNSIGNED_WINDOWS_PREVIEW_LANE is True
    assert launcher.legacy.RECEIPT_CONTRACT == (
        "chummer6-ui.preview-nightly-unsigned-jit-launch"
    )
    assert launcher.legacy.EXPECTED_CONTENT_DIRECTORIES == (
        "publication",
        "provenance",
        "publication/files",
        "provenance/config",
        "provenance/retained-windows-publish-closure",
    )


def test_trusted_snapshot_loader_uses_committed_composition_and_scope_bytes() -> None:
    exporter_source = (
        ROOT / "scripts" / "preview_nightly_unsigned_candidate_export.py"
    ).read_bytes()
    scope_source = (
        ROOT / "scripts" / "preview_nightly_unsigned_scope.py"
    ).read_bytes()
    composition_source = (
        ROOT / "scripts" / "preview_nightly_unsigned_composition.py"
    ).read_bytes()
    module = launcher.load_unsigned_exporter(
        exporter_source, composition_source, (("scope", scope_source),)
    )
    assert module.PRODUCER_WORKFLOW == launcher.WORKFLOW_PATH
    assert module.PUBLICATION_SCOPE.CONTRACT_NAME == (
        "chummer6-ui.preview-nightly-unsigned-publication-scope"
    )
    assert module.PUBLICATION_SCOPE.CONTRACT_VERSION == 3
    assert module.COMPOSITION.CONTRACT_NAME == (
        "chummer6-ui.preview-nightly-unsigned-composition-request"
    )
    assert module.COMPOSITION.CONTRACT_VERSION == 3


def test_governed_subset_materialization_accepts_only_export_content(
    tmp_path: Path,
) -> None:
    values = fixtures.fixture(tmp_path)
    exporter = fixtures.exporter
    subset = tmp_path / "private" / "candidate-input"
    subset.parent.mkdir()
    candidate = launcher.legacy.materialize_candidate_subset(
        values["candidate"],
        subset,
        exporter,
        fixtures.scope_fixtures.SOURCE_SHA,
    )
    assert candidate.version == fixtures.scope_fixtures.VERSION
    assert candidate.manifest_sha256 == values["args"].expected_manifest_sha256
    exporter.require_exact_tree(
        candidate.root, exporter.CONTENT_PATHS, "governed candidate subset"
    )
    assert all((candidate.root / path).stat().st_mode & 0o222 == 0 for path in exporter.CONTENT_PATHS)


def test_wrapper_snapshots_itself_exporter_composition_and_scope() -> None:
    source = (
        ROOT / "scripts" / "preview_nightly_unsigned_jit_launcher.py"
    ).read_text(encoding="utf-8")
    assert '"scripts/preview_nightly_unsigned_jit_launcher.py"' in source
    assert 'EXPORTER_PATH = "scripts/preview_nightly_unsigned_candidate_export.py"' in source
    assert 'SCOPE_PATH = "scripts/preview_nightly_unsigned_scope.py"' in source
    assert 'COMPOSITION_PATH = "scripts/preview_nightly_unsigned_composition.py"' in source
    assert "committed_file_snapshot" in source


def test_shell_entrypoint_has_no_release_actions() -> None:
    source = (
        ROOT / "scripts" / "run-preview-nightly-unsigned-jit-launcher.sh"
    ).read_text(encoding="utf-8")
    assert "preview_nightly_unsigned_jit_launcher.py" in source
    for forbidden in (
        "publish",
        "deploy",
        "windows-native-evidence-capture",
        "sign-windows",
    ):
        assert forbidden not in source
