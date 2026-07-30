from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh"


def test_source_snapshot_materializes_a_complete_isolated_workspace() -> None:
    source = SCRIPT.read_text(encoding="utf-8")

    assert (
        'SOURCE_SNAPSHOT_WORKSPACE_ROOT="$(mktemp -d '
        '"$source_snapshot_parent/workspace.XXXXXX")'
    ) in source
    assert (
        'SOURCE_SNAPSHOT_ROOT="$SOURCE_SNAPSHOT_WORKSPACE_ROOT/chummer-presentation"'
        in source
    )
    for repository in (
        "chummer-core-engine",
        "chummer.run-services",
        "chummer-hub-registry",
        "chummer-ui-kit",
        "fleet/repos/chummer-media-factory",
    ):
        assert repository in source

    assert '"connected_repositories": connected_repository_snapshots' in source
    assert '"snapshot_workspace_root": str(snapshot_root.parent)' in source
    assert 'git", "-C", str(connected_root), "rev-parse", "HEAD"' in source
    assert '"entries_path": str(connected_entries_path)' in source
    assert "connected_identity_stable" in source


def test_source_snapshot_cleanup_removes_the_unique_workspace() -> None:
    source = SCRIPT.read_text(encoding="utf-8")

    assert 'rm -rf "$SOURCE_SNAPSHOT_WORKSPACE_ROOT"' in source
    cleanup_start = source.index("cleanup_snapshot() {")
    cleanup_end = source.index("\n}\n", cleanup_start)
    cleanup = source[cleanup_start:cleanup_end]
    assert 'rm -rf "$SOURCE_SNAPSHOT_ROOT"' not in cleanup
