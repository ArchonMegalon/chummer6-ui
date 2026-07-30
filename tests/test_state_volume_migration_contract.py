from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_state_mode_normalizer_is_content_bound_and_mount_scoped() -> None:
    source = (ROOT / "Docker" / "normalize-state-volume-modes.sh").read_text(
        encoding="utf-8"
    )

    assert 'state_root="/app/state"' in source
    assert "/proc/self/mountinfo" in source
    assert 'find "$state_root" -xdev -type l' in source
    assert 'find "$state_root" -xdev ! -type d ! -type f' in source
    assert 'find "$state_root" -xdev -type d -exec chmod 0700 {} +' in source
    assert 'find "$state_root" -xdev -type f -exec chmod 0600 {} +' in source
    assert '[ "$before_digest" = "$after_digest" ]' in source
    assert '"contentSha256":"%s"' in source


def test_migration_runbook_restores_only_required_one_shot_capabilities() -> None:
    runbook = (ROOT / "docs" / "CONTAINER_RUNTIME_HARDENING.md").read_text(
        encoding="utf-8"
    )
    compose = (ROOT / "docker-compose.yml").read_text(encoding="utf-8")

    assert "cap_drop:" in compose
    assert "- ALL" in compose
    assert runbook.count("--cap-add DAC_OVERRIDE --cap-add FOWNER") == 3
    assert runbook.count("--cap-add DAC_OVERRIDE --cap-add CHOWN") == 3
    assert "both one-shot tools" in runbook
    assert "same content SHA-256" in runbook


def test_runtime_images_ship_both_reviewed_state_tools() -> None:
    for dockerfile_name in ("Chummer.Api/Dockerfile", "Chummer.Blazor/Dockerfile"):
        dockerfile = (ROOT / dockerfile_name).read_text(encoding="utf-8")
        assert "chummer-state-mode-normalization" in dockerfile
        assert "chummer-state-ownership-migration" in dockerfile
