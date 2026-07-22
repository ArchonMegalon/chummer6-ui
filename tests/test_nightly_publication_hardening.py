from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]


def script_text(name: str) -> str:
    return (REPO_ROOT / "scripts" / name).read_text(encoding="utf-8")


def test_generic_nightly_no_longer_uses_forced_visual_handoff_publish_exception() -> None:
    nightly = script_text("publish-latest-nightly-to-downloads.sh")
    bundle = script_text("publish-download-bundle.sh")

    assert "forced_preview_nightly_visual_handoff_allowed()" not in nightly
    assert "Forced preview nightly publication continuing" not in nightly
    assert "forced_preview_nightly_visual_handoff_allowed()" in bundle
    assert 'if ! to_bool "$FORCE_NIGHTLY_PUBLISH"; then' in bundle
    assert 'blockers != [ALLOWED_BLOCKER]' in bundle
    assert 'normalize(handoff.get("channel")) != "preview"' in bundle
    assert 'handoff.get("stage_proof_complete") is not False' in bundle
    assert 'normalize(visual.get("status")) != "ready_for_windows_host"' in bundle
    assert 'visual.get("only_blocker_is_visual_proof") is not True' in bundle


def test_forced_visual_exception_cannot_bypass_stable_channel_checks() -> None:
    nightly = script_text("publish-latest-nightly-to-downloads.sh")
    bundle = script_text("publish-download-bundle.sh")

    assert 'ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH="${CHUMMER_ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH:-0}"' in nightly
    assert "Nightly publisher is the preview handoff lane. Refusing stable/public_stable publication from this script." in nightly
    assert 'if [[ "$release_channel" != "preview" ]]; then' in bundle
    assert 'manifest_channel_is_preview "$deploy_dir/RELEASE_CHANNEL.generated.json"' in bundle
    assert "ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH" not in nightly
    assert "ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH" not in bundle


def test_public_nightly_requires_shared_policy_eligible_installer_before_windows_gates() -> None:
    nightly = script_text("publish-latest-nightly-to-downloads.sh")

    assert "verify_public_nightly_installer_eligibility()" in nightly
    assert "verify-public-nightly-installer-eligibility.py" in nightly
    assert ".codex-design/product/DESKTOP_PLATFORM_ACCEPTANCE_MATRIX.yaml" in nightly
    assert "Public nightly requires at least one staged open-public Windows/Linux installer" in nightly
    assert 'verify_public_nightly_installer_eligibility "$latest_stage"' in nightly
    assert nightly.index('verify_latest_stage_artifact_scope_gate "$latest_stage"') < nightly.index(
        'verify_public_nightly_installer_eligibility "$latest_stage"'
    )
    assert nightly.index('verify_public_nightly_installer_eligibility "$latest_stage"') < nightly.index(
        'verify_latest_stage_windows_payload_gate "$latest_stage"'
    )
    assert nightly.index('verify_public_nightly_installer_eligibility "$latest_stage"') < nightly.index(
        'bash "$SCRIPT_DIR/publish-download-bundle.sh" "$latest_stage/publication" "$DEPLOY_DIR"'
    )


def test_support_proof_only_handoff_exits_before_publication() -> None:
    nightly = script_text("publish-latest-nightly-to-downloads.sh")

    assert 'SUPPORT_PROOF_ONLY_HANDOFF="${CHUMMER_NIGHTLY_SUPPORT_PROOF_ONLY_HANDOFF:-0}"' in nightly
    assert "Support/proof-only handoff mode active; public nightly cadence and publication are disabled." in nightly
    assert "Prepared support/proof-only nightly handoff:" in nightly
    assert "Public downloads shelf unchanged; no public nightly was published." in nightly
    support_branch = nightly.index('if to_bool "$SUPPORT_PROOF_ONLY_HANDOFF"; then\n  emit_windows_visual_proof_handoff_guidance')
    public_gate = nightly.index('verify_public_nightly_installer_eligibility "$latest_stage"')
    publish = nightly.index(
        'bash "$SCRIPT_DIR/publish-download-bundle.sh" '
        '"$latest_stage/publication" "$DEPLOY_DIR"'
    )
    assert support_branch < public_gate < publish
    assert 'echo "Public downloads shelf unchanged; no public nightly was published."\n  exit 0' in nightly


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


def test_nightly_publisher_retries_public_edge_routes_during_restart_warmup() -> None:
    nightly = script_text("publish-latest-nightly-to-downloads.sh")

    assert 'PUBLIC_EDGE_VERIFY_ATTEMPTS="${CHUMMER_PUBLIC_EDGE_VERIFY_ATTEMPTS:-20}"' in nightly
    assert (
        'PUBLIC_EDGE_VERIFY_RETRY_DELAY_SECONDS="${CHUMMER_PUBLIC_EDGE_VERIFY_RETRY_DELAY_SECONDS:-2}"'
        in nightly
    )
    assert 'urllib.request.Request(f"{base_url}{route}", method="GET", headers=headers)' in nightly
    assert 'for attempt in range(1, attempts + 1):' in nightly
    assert 'if status in {500, 502, 503, 504} and attempt < attempts:' in nightly
    assert 'time.sleep(retry_delay_seconds)' in nightly
    assert 'method="HEAD"' not in nightly


def test_linux_deb_stage_normalizes_package_metadata_permissions() -> None:
    installer = script_text("build-desktop-installer.sh")

    assert 'find "$stage_root" -type d -exec chmod 0755 {} +' in installer
    assert 'chmod 0644 "$stage_root/DEBIAN/control" "$desktop_path"' in installer
    assert installer.index('chmod 0644 "$stage_root/DEBIAN/control" "$desktop_path"') < installer.index(
        'chmod 0755 "$stage_root/DEBIAN/postinst"'
    )
