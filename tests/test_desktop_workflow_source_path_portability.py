from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MATERIALIZER = ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-workflow-execution-gate.sh"


def test_historical_source_test_paths_are_remapped_inside_current_repo() -> None:
    materializer = MATERIALIZER.read_text(encoding="utf-8")

    assert "HISTORICAL_PRESENTATION_REPO_ROOTS" in materializer
    assert '"/docker/chummercomplete/chummer6-ui"' in materializer
    assert '"/docker/chummercomplete/chummer-presentation"' in materializer
    assert "resolve_source_test_file_path" in materializer
    assert "raw_path.relative_to(historical_root)" in materializer
    assert "path_within_root(remapped, root) and remapped.is_file()" in materializer


def test_source_test_path_receipt_records_original_and_resolved_identity() -> None:
    materializer = MATERIALIZER.read_text(encoding="utf-8")

    assert 'evidence["flagship_head_source_test_file_paths"]' in materializer
    assert 'evidence["flagship_head_source_test_file_resolved_paths"]' in materializer
    assert 'evidence["flagship_head_source_test_file_remapped"]' in materializer
