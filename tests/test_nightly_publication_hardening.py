from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]


def script_text(name: str) -> str:
    return (REPO_ROOT / "scripts" / name).read_text(encoding="utf-8")


def test_forced_preview_exception_requires_the_sole_native_windows_visual_blocker() -> None:
    nightly = script_text("publish-latest-nightly-to-downloads.sh")
    bundle = script_text("publish-download-bundle.sh")

    for publisher in (nightly, bundle):
        assert "forced_preview_nightly_visual_handoff_allowed()" in publisher
        assert 'if ! to_bool "$FORCE_NIGHTLY_PUBLISH"; then' in publisher
        assert 'blockers != [ALLOWED_BLOCKER]' in publisher
        assert 'normalize(handoff.get("channel")) != "preview"' in publisher
        assert 'handoff.get("stage_proof_complete") is not False' in publisher
        assert 'normalize(visual.get("status")) != "ready_for_windows_host"' in publisher
        assert 'visual.get("only_blocker_is_visual_proof") is not True' in publisher
        assert (
            "Forced preview nightly publication continuing with Windows visual proof handoff only; "
            "stable promotion remains blocked."
        ) in publisher


def test_forced_visual_exception_cannot_bypass_stable_channel_checks() -> None:
    nightly = script_text("publish-latest-nightly-to-downloads.sh")
    bundle = script_text("publish-download-bundle.sh")

    assert 'if [[ "$normalized_public_release_channel" != "preview" ]]; then' in nightly
    assert 'if [[ "$release_channel" != "preview" ]]; then' in bundle
    assert 'manifest_channel_is_preview "$deploy_dir/RELEASE_CHANNEL.generated.json"' in bundle
    assert "ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH" not in nightly
    assert "ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH" not in bundle


def test_scoped_preview_generation_does_not_rehydrate_other_release_artifacts() -> None:
    generator = script_text("generate-releases-manifest.sh")

    bundle = script_text("publish-download-bundle.sh")
    nightly = script_text("publish-latest-nightly-to-downloads.sh")

    assert 'SCOPE_TO_STAGE_ARTIFACTS="${CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS:-0}"' in generator
    assert 'SCOPE_TO_STAGE_ARTIFACTS="${CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS:-0}"' in bundle
    assert "CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS=1" in nightly
    assert 'CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS="$SCOPE_TO_STAGE_ARTIFACTS"' in bundle
    assert "scoped stage artifacts active; skipped registry startup-smoke hydration" in generator
    assert "scoped stage artifacts active; skipped registry manifest fallback restore" in generator
    assert "scoped stage artifacts active; skipped proof-backed quarantined installer promotion" in generator
    assert '"$SCOPE_TO_STAGE_ARTIFACTS"' in generator
    assert 'if to_bool "$SCOPE_TO_STAGE_ARTIFACTS"; then\n    candidate_path="$DOWNLOADS_DIR/$file_name"' in generator
    assert "receipt_path.unlink(missing_ok=True)" in generator
    assert "if digest and sha256_file(staged_artifact_path) != digest:" in generator


def test_release_generation_can_bind_an_explicit_flagship_readiness_receipt() -> None:
    generator = script_text("generate-releases-manifest.sh")

    assert (
        'FLAGSHIP_READINESS_PATH="${CHUMMER_FLAGSHIP_READINESS_PATH:-${CHUMMER_FLAGSHIP_PRODUCT_READINESS_RECEIPT_PATH:-}}"'
        in generator
    )
    assert 'if [[ -n "$FLAGSHIP_READINESS_PATH" ]]; then' in generator
    assert 'if [[ ! -f "$FLAGSHIP_READINESS_PATH" ]]; then' in generator
    assert 'if [[ "$materializer_help" != *"--flagship-readiness"* ]]; then' in generator
    assert 'materialize_args+=(--flagship-readiness "$FLAGSHIP_READINESS_PATH")' in generator


def test_linux_deb_stage_normalizes_package_metadata_permissions() -> None:
    installer = script_text("build-desktop-installer.sh")

    assert 'find "$stage_root" -type d -exec chmod 0755 {} +' in installer
    assert 'chmod 0644 "$stage_root/DEBIAN/control" "$desktop_path"' in installer
    assert installer.index('chmod 0644 "$stage_root/DEBIAN/control" "$desktop_path"') < installer.index(
        'chmod 0755 "$stage_root/DEBIAN/postinst"'
    )
