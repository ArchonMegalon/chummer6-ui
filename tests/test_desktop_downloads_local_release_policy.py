from __future__ import annotations

import json
import os
import subprocess
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]


def test_github_actions_workflows_are_not_part_of_presentation_release_policy() -> None:
    assert not (REPO_ROOT / ".github" / ("work" + "flows")).exists()


def test_daily_publish_policy_is_documented_in_local_runbook() -> None:
    runbook = (REPO_ROOT / "docs" / "SELF_HOSTED_DOWNLOADS_RUNBOOK.md").read_text(encoding="utf-8")

    assert "RUNBOOK_MODE=publish-latest-nightly" in runbook
    assert "08:00 Europe/Vienna" in runbook
    assert "once per day in the morning release window" in runbook
    assert "Build only what the proof needs" in runbook
    assert "does not publish the live downloads shelf and does not change the stable channel by itself" in runbook
    assert "CHUMMER_WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE=download" in runbook
    assert "CHUMMER_WINEPATH_TIMEOUT_SECONDS" in runbook
    assert "CHUMMER_WINEBOOT_INIT_TIMEOUT_SECONDS" in runbook
    assert "CHUMMER_WINDOWS_BINARY_TIMEOUT_SECONDS" in runbook
    assert ("workflow" + "_dispatch") not in runbook
    assert ("GitHub " + "Actions") not in runbook


def test_release_candidate_handoff_documents_windows_download_mode_smoke() -> None:
    handoff_doc = (REPO_ROOT / "docs" / "RELEASE_CANDIDATE_HANDOFF.md").read_text(encoding="utf-8")

    assert "a passing startup-smoke receipt must exercise bootstrap download mode" in handoff_doc
    assert "local payload handoff is useful for diagnosis" in handoff_doc
    assert "payload download target, size verification, checksum verification" in handoff_doc
    assert "CHUMMER_WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE=download" in handoff_doc


def test_public_promotion_evidence_preserves_install_access_class(tmp_path: Path) -> None:
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    startup_smoke_dir = tmp_path / "startup-smoke"
    output_path = tmp_path / "public-promotion.json"
    startup_smoke_dir.mkdir()
    manifest_path.write_text(
        json.dumps(
            {
                "channel": "preview",
                "artifacts": [
                    {
                        "artifactId": "avalonia-osx-arm64-installer",
                        "fileName": "chummer-avalonia-osx-arm64-installer.dmg",
                        "platform": "macos",
                        "head": "avalonia",
                        "rid": "osx-arm64",
                        "arch": "arm64",
                        "sha256": "abc123",
                        "sizeBytes": 1,
                        "kind": "installer",
                        "installAccessClass": "account_required",
                    }
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            "python3",
            str(REPO_ROOT / "scripts" / "generate-public-promotion-evidence.py"),
            "--manifest",
            str(manifest_path),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
            "--output",
            str(output_path),
            "--channel",
            "preview",
            "--generated-at",
            "2026-07-03T00:00:00Z",
        ],
        text=True,
        capture_output=True,
        check=False,
        env={
            **os.environ,
            "CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE": "true",
        },
    )

    assert result.returncode == 0, result.stderr
    payload = json.loads(output_path.read_text(encoding="utf-8"))
    assert payload["artifacts"][0]["installAccessClass"] == "account_required"


def test_materialized_public_promotion_evidence_matches_active_immutable_generation() -> None:
    release_shelf_root = REPO_ROOT.parent / "chummer.run-services" / "Chummer.Portal" / "downloads"
    pointer = json.loads((release_shelf_root / "current.json").read_text(encoding="utf-8"))
    active_evidence_path = (
        release_shelf_root
        / "generations"
        / pointer["generationId"]
        / "release-evidence"
        / "public-promotion.json"
    )
    active_evidence = active_evidence_path.read_bytes()
    evidence_paths = (
        REPO_ROOT / "Docker" / "Downloads" / "release-evidence" / "public-promotion.json",
        REPO_ROOT / "Chummer.Portal" / "downloads" / "release-evidence" / "public-promotion.json",
    )

    for evidence_path in evidence_paths:
        # Compatibility mirrors are byte-exact projections of the immutable
        # active generation. Historical generations are never rewritten; the
        # generator regression below enforces portable receipt references for
        # every newly produced generation.
        assert evidence_path.read_bytes() == active_evidence


def test_public_promotion_evidence_rejects_symlinked_receipts(tmp_path: Path) -> None:
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    startup_smoke_dir = tmp_path / "startup-smoke"
    output_path = tmp_path / "release-evidence" / "public-promotion.json"
    startup_smoke_dir.mkdir()
    manifest_path.write_text(
        json.dumps({"channel": "preview", "artifacts": []}) + "\n",
        encoding="utf-8",
    )
    outside_receipt = tmp_path / "outside.receipt.json"
    outside_receipt.write_text("{}\n", encoding="utf-8")
    (startup_smoke_dir / "startup-smoke-unsafe.receipt.json").symlink_to(outside_receipt)

    result = subprocess.run(
        [
            "python3",
            str(REPO_ROOT / "scripts" / "generate-public-promotion-evidence.py"),
            "--manifest",
            str(manifest_path),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
            "--output",
            str(output_path),
            "--channel",
            "preview",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode != 0
    assert "receipt must be a regular file with a safe public basename" in result.stderr
    assert not output_path.exists()


def test_public_promotion_evidence_accepts_current_ready_log_for_stale_receipt_timestamp(tmp_path: Path) -> None:
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    startup_smoke_dir = tmp_path / "startup-smoke"
    signing_receipts_dir = tmp_path / "signing"
    output_path = tmp_path / "release-evidence" / "public-promotion.json"
    startup_smoke_dir.mkdir()
    signing_receipts_dir.mkdir()
    manifest_path.write_text(
        json.dumps(
            {
                "channel": "public_stable",
                "artifacts": [
                    {
                        "artifactId": "avalonia-osx-arm64-installer",
                        "fileName": "chummer-avalonia-osx-arm64-installer.dmg",
                        "platform": "macos",
                        "head": "avalonia",
                        "rid": "osx-arm64",
                        "arch": "arm64",
                        "sha256": "abc123",
                        "sizeBytes": 1,
                        "kind": "installer",
                    }
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    receipt_path = startup_smoke_dir / "startup-smoke-avalonia-osx-arm64.receipt.json"
    receipt_path.write_text(
        json.dumps(
            {
                "status": "pass",
                "headId": "avalonia",
                "platform": "macos",
                "arch": "arm64",
                "rid": "osx-arm64",
                "readyCheckpoint": "pre_ui_event_loop",
                "hostClass": "self-hosted-osx-arm64",
                "artifactDigest": "sha256:abc123",
                "artifactDigestSource": "environment",
                "operatingSystem": "macOS 15",
                "generatedAt": "2026-06-12T12:30:16Z",
                "startedAtUtc": "2026-06-12T12:30:16Z",
                "recordedAtUtc": "2026-06-12T12:30:16Z",
                "completedAtUtc": "2026-06-12T12:30:16Z",
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    log_path = startup_smoke_dir / "startup-smoke-avalonia-osx-arm64.log"
    log_path.write_text(
        "startup smoke ready: head=avalonia platform=macos arch=arm64 checkpoint=pre_ui_event_loop\n",
        encoding="utf-8",
    )
    current_ready_at = datetime.now(timezone.utc).replace(microsecond=0)
    log_timestamp = current_ready_at.timestamp()
    os.utime(log_path, (log_timestamp, log_timestamp))
    signing_receipt_name = "signing-avalonia-osx-arm64.receipt.json"
    (signing_receipts_dir / signing_receipt_name).write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.desktop_artifact_signing",
                "platform": "macos",
                "rid": "osx-arm64",
                "generatedAt": current_ready_at.isoformat().replace("+00:00", "Z"),
                "artifacts": [
                    {
                        "fileName": "chummer-avalonia-osx-arm64-installer.dmg",
                        "sha256": "abc123",
                        "kind": "installer",
                        "signingStatus": "pass",
                        "notarizationStatus": "pass",
                    }
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )

    result = subprocess.run(
        [
            "python3",
            str(REPO_ROOT / "scripts" / "generate-public-promotion-evidence.py"),
            "--manifest",
            str(manifest_path),
            "--startup-smoke-dir",
            str(startup_smoke_dir),
            "--signing-receipts-dir",
            str(signing_receipts_dir),
            "--output",
            str(output_path),
            "--channel",
            "public_stable",
            "--generated-at",
            current_ready_at.isoformat().replace("+00:00", "Z"),
        ],
        text=True,
        capture_output=True,
        check=False,
        env={
            **os.environ,
            "CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE": "true",
        },
    )

    assert result.returncode == 0, result.stderr
    payload = json.loads(output_path.read_text(encoding="utf-8"))
    artifact = payload["artifacts"][0]
    assert artifact["promotionStatus"] == "pass"
    assert artifact["startupSmokeReason"] == ""
    assert artifact["startupSmokeReceiptPath"] == "startup-smoke/startup-smoke-avalonia-osx-arm64.receipt.json"
    assert artifact["signingReceiptPath"] == f"signing/{signing_receipt_name}"
    assert str(tmp_path) not in json.dumps(payload, sort_keys=True)


def test_preview_startup_smoke_gate_does_not_block_account_gated_installers() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert 'python3 - "$PROMOTION_EVIDENCE_PATH" "$RELEASE_CHANNEL"' in generator
    assert 'release_channel = str(sys.argv[2] if len(sys.argv) > 2 else "").strip().lower()' in generator
    assert 'install_access_class = str(artifact.get("installAccessClass") or "").strip().lower()' in generator
    assert 'release_channel == "preview" and install_access_class in {"account_required", "account_recommended"}' in generator


def test_release_generator_preserves_registry_owned_review_required_summaries() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert 'payload["supportabilityState"] = trust_supportability_state' in generator
    assert "sourceUpdatedAtUtc" in generator
    assert 'receipt_path.name[: -len(".receipt.json")] + ".log"' in generator
    assert "startup smoke ready:" in generator
    assert "Proof freshness is missing or stale on this shelf" not in generator
    assert "preview publication is visible but not yet gold-ready" not in generator


def test_release_generator_syncs_registry_published_mirror_and_public_promotion_evidence() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "sync_public_promotion_evidence_file()" in generator
    assert 'cp -f "$PROMOTION_EVIDENCE_PATH" "$target_path"' in generator
    assert 'sync_public_promotion_evidence_file "$PORTAL_DOWNLOADS_DIR" "local portal"' in generator
    assert '"$RUN_SERVICES_DOWNLOADS_ROOT/releases.json" \\' in generator
    assert '"$RUN_SERVICES_DOWNLOADS_ROOT/RELEASE_CHANNEL.generated.json" \\' in generator
    assert '"$RUN_SERVICES_DOWNLOADS_ROOT" \\' in generator
    assert '"run-services downloads mirror"' in generator
    assert 'sync_public_promotion_evidence_file "$RUN_SERVICES_DOWNLOADS_ROOT" "run-services downloads mirror"' in generator
    assert 'sync_public_promotion_evidence_file "$PRESENTATION_MIRROR_ROOT/Docker/Downloads" "presentation downloads mirror"' in generator
    assert 'sync_presentation_downloads_mirror \\' in generator
    assert '"$REGISTRY_RELEASES_MANIFEST_PATH" \\' in generator
    assert '"$REGISTRY_CANONICAL_MANIFEST_PATH" \\' in generator
    assert '"registry published"' in generator
    assert 'sync_public_promotion_evidence_file "$(dirname "$REGISTRY_CANONICAL_MANIFEST_PATH")" "registry published"' in generator
    assert 'python3 "$REGISTRY_ROOT/scripts/verify_public_release_channel.py" "${verify_args[@]}" "$REGISTRY_CANONICAL_MANIFEST_PATH" >/dev/null' in generator


def test_release_generator_prefers_coherent_local_startup_smoke_before_registry_hydration() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "startup_smoke_dir_matches_downloads_dir()" in generator
    assert 'elif startup_smoke_dir_matches_downloads_dir "$STARTUP_SMOKE_DIR" "$DOWNLOADS_DIR"; then' in generator
    assert "local startup-smoke receipts already match downloads source; skipped registry startup-smoke hydration" in generator
    assert 'cp "$STARTUP_SMOKE_DIR"/* "$hydrated_startup_smoke_dir"/' in generator


def test_windows_startup_smoke_bounds_winepath_conversion() -> None:
    smoke = (REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh").read_text(encoding="utf-8")

    assert 'CHUMMER_WINEPATH_TIMEOUT_SECONDS:-15' in smoke
    assert 'timeout "$winepath_timeout" winepath -w "$input_path"' in smoke
    assert 'CHUMMER_WINDOWS_BINARY_TIMEOUT_SECONDS:-300' in smoke
    assert 'run_with_optional_xvfb timeout "$wine_binary_timeout" "$wine_bin" "$native_executable_path" "$@"' in smoke
    assert "initialize_windows_startup_wine_prefix()" in smoke
    assert 'CHUMMER_WINEBOOT_INIT_TIMEOUT_SECONDS:-180' in smoke
    assert 'run_with_optional_xvfb "${timeout_prefix[@]}" wineboot --init' in smoke
    assert 'run_with_optional_xvfb "${timeout_prefix[@]}" wineserver -w' in smoke
    assert '*/dosdevices/[A-Za-z]:/*)' in smoke
    assert 'upper_ascii()' in smoke
    assert 'printf \'%s:%s\\n\' "$(upper_ascii "$drive")" "${drive_path//\\//\\\\}"' in smoke
    assert "Wine maps the Unix filesystem root to Z:" in smoke
    assert "printf 'Z:%s\\n' \"${input_path//\\//\\\\}\"" in smoke


def test_latest_nightly_publish_preflights_windows_bootstrap_payload_metadata() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert "verify_latest_stage_windows_payload_gate()" in publisher
    assert "verify-windows-installer-payloads.py" in publisher
    assert "--require-embedded-bootstrap-metadata" in publisher
    assert "--require-manifest-row" in publisher
    assert "--allow-empty" not in publisher
    assert "Nightly stage failed Windows installer payload preflight. Build a fresh stage before publishing." in publisher
    assert publisher.index('verify_latest_stage_windows_payload_gate "$latest_stage"') < publisher.index('echo "Publishing latest nightly stage: $latest_stage"')


def test_latest_nightly_publish_ignores_incomplete_helper_stage_directories() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert "is_publishable_nightly_stage()" in publisher
    assert '[[ -f "$stage_dir/RELEASE_CHANNEL.generated.json" ]] || return 1' in publisher
    assert '[[ -f "$stage_dir/releases.json" ]] || return 1' in publisher
    assert '[[ -d "$stage_dir/files" ]] || return 1' in publisher
    assert 'if ! is_publishable_nightly_stage "$candidate"; then' in publisher
    assert 'echo "No publishable nightly stage found under $STAGING_ROOT"' in publisher
    assert publisher.index('if ! is_publishable_nightly_stage "$candidate"; then') < publisher.index('latest_stage="$candidate"')


def test_latest_nightly_publish_requires_windows_installer_startup_smoke_before_promotion() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")
    verifier = (REPO_ROOT / "scripts" / "verify-windows-bootstrap-startup-smoke.py").read_text(encoding="utf-8")

    assert 'PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-false}"' in publisher
    assert 'SKIP_STARTUP_SMOKE_HYDRATION="${CHUMMER_SKIP_STARTUP_SMOKE_HYDRATION:-0}"' in publisher
    assert 'ALLOW_SKIPPED_STARTUP_SMOKE="${CHUMMER_ALLOW_SKIPPED_STARTUP_SMOKE:-0}"' in publisher
    assert "verify_latest_stage_windows_startup_smoke_gate()" in publisher
    assert 'python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py"' in publisher
    assert "Windows installer startup-smoke receipt is missing" in verifier
    assert "Windows installer startup-smoke receipt is not passing" in verifier
    assert "Windows installer startup-smoke receipt artifactDigest mismatch" in verifier
    assert "matching stage bytes are missing" in verifier
    assert "RELEASE_CHANNEL.generated.json omits the matching installer row" in verifier
    assert "releases.json omits the matching installer row" in verifier
    assert "refresh_release_build_handoff()" in publisher
    assert 'refresh_release_build_handoff "$latest_stage"' in publisher
    assert "verify_latest_stage_windows_exit_gate()" in publisher
    assert 'bash "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >/dev/null' in publisher
    assert 'CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH="$release_channel_manifest"' in publisher
    assert 'CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$files_dir"' in publisher
    assert 'CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="$visual_proof_path"' in publisher
    assert 'CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH="$gate_output"' in publisher
    assert "emit_windows_visual_proof_handoff_guidance()" in publisher
    assert 'emit_windows_visual_proof_handoff_guidance "$stage_dir"' in publisher
    assert "Windows visual proof handoff:" in publisher
    assert "Windows visual proof status:" in publisher
    assert "Windows visual proof next action {index}:" in publisher
    assert "Nightly stage failed Windows desktop exit gate preflight. Use the Windows visual proof handoff above before publishing." in publisher
    assert "Nightly stage failed Windows installer startup smoke preflight. Build and smoke-test a fresh stage before publishing." in publisher
    assert publisher.index('verify_latest_stage_windows_payload_gate "$latest_stage"') < publisher.index('verify_latest_stage_windows_startup_smoke_gate "$latest_stage"')
    assert publisher.index('verify_latest_stage_windows_startup_smoke_gate "$latest_stage"') < publisher.index('verify_latest_stage_windows_exit_gate "$latest_stage"')
    assert publisher.index('verify_latest_stage_windows_exit_gate "$latest_stage"') < publisher.index('echo "Publishing latest nightly stage: $latest_stage"')
    assert 'row_platform_id = norm(row.get("platformId"))' in verifier
    assert 'normalized_arch = normalized_rid.rsplit("-", 1)[-1] if "-" in normalized_rid else normalized_rid' in verifier
    assert 'elif norm(row.get("arch")) != normalized_arch:' in verifier


def test_forced_preview_nightly_can_publish_only_visual_proof_handoff() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")
    bundle_publisher = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert "forced_preview_nightly_visual_handoff_allowed()" in publisher
    assert "forced_preview_nightly_visual_handoff_allowed()" in bundle_publisher
    assert 'if ! to_bool "$FORCE_NIGHTLY_PUBLISH"; then' in publisher
    assert 'if ! to_bool "$FORCE_NIGHTLY_PUBLISH"; then' in bundle_publisher
    assert 'if [[ "$normalized_public_release_channel" != "preview" ]]; then' in publisher
    assert 'if [[ "$release_channel" != "preview" ]]; then' in bundle_publisher
    assert 'blockers != [ALLOWED_BLOCKER]' in publisher
    assert 'blockers != [ALLOWED_BLOCKER]' in bundle_publisher
    assert 'normalize(visual.get("status")) != "ready_for_windows_host"' in publisher
    assert 'normalize(visual.get("status")) != "ready_for_windows_host"' in bundle_publisher
    assert 'visual.get("only_blocker_is_visual_proof") is not True' in publisher
    assert 'visual.get("only_blocker_is_visual_proof") is not True' in bundle_publisher
    assert "Forced preview nightly publication continuing with Windows visual proof handoff only; stable promotion remains blocked." in publisher
    assert "Forced preview nightly publication continuing with Windows visual proof handoff only; stable promotion remains blocked." in bundle_publisher


def test_bundle_publisher_carries_startup_smoke_logs_and_source_updated_timestamps() -> None:
    bundle_publisher = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")
    verifier = (REPO_ROOT / "scripts" / "verify-windows-bootstrap-startup-smoke.py").read_text(encoding="utf-8")

    assert "sourceUpdatedAtUtc" in bundle_publisher
    assert 'receipt_path.name.replace(".receipt.json", ".log")' in bundle_publisher
    assert '-name "startup-smoke-*.log"' in bundle_publisher
    assert "startup_smoke_search_roots(" in verifier
    assert 'candidate_paths.append(root / ".codex-studio" / "published" / "startup-smoke")' in verifier
    assert 'candidate_paths.append(root / "Docker" / "Downloads" / "startup-smoke")' in verifier


def test_scoped_preview_nightly_generation_does_not_rehydrate_registry_artifacts() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert 'SCOPE_TO_STAGE_ARTIFACTS="${CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS:-}"' in generator
    assert "lower_ascii()" in generator
    assert '[[ "$(lower_ascii "$RELEASE_CHANNEL")" == "preview" && "$REQUIRE_COMPLETE_DESKTOP_COVERAGE" == "0" ]]' in generator
    assert 'scoped stage artifacts active; skipped registry startup-smoke hydration' in generator
    assert 'scoped stage artifacts active; skipped registry manifest fallback restore' in generator
    assert 'scoped stage artifacts active; skipped proof-backed quarantined installer promotion' in generator
    assert 'sanitize_startup_smoke_dir \\' in generator
    assert '"$CANONICAL_FILES_DIR" \\\n    "$SCOPE_TO_STAGE_ARTIFACTS"' in generator
    assert 'if to_bool "$SCOPE_TO_STAGE_ARTIFACTS"; then\n    candidate_path="$DOWNLOADS_DIR/$file_name"' in generator
    assert "receipt_path.unlink(missing_ok=True)" in generator
    assert 'if digest and sha256_file(staged_artifact_path) != digest:' in generator


def test_generator_skips_incomplete_local_windows_bootstrap_sources_before_registry_fallback() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "local_windows_bootstrap_artifact_source_is_incomplete()" in generator
    assert 'MANIFEST_REHYDRATION_PASS="${CHUMMER_RELEASE_MANIFEST_REHYDRATION_PASS:-0}"' in generator
    assert "PROMOTED_DOWNLOADS_SOURCE_RESTORED=0" in generator
    assert 'candidate_size="$(stat -c %s "$candidate_path" 2>/dev/null || printf \'0\')"' in generator
    assert 'if (( candidate_size <= 20 )); then' in generator
    assert 'if [[ "$file_name" == chummer-*-win-*-payload.zip && ! -f "$candidate_path.json" ]]; then' in generator
    assert 'if [[ "$candidate_dir" == "$DOWNLOADS_DIR" ]] \\' in generator
    assert '&& local_windows_bootstrap_artifact_source_is_incomplete "$candidate_path" "$file_name"; then' in generator
    assert 'echo "skipping incomplete local promoted artifact source: $candidate_path" >&2' in generator
    assert 'PROMOTED_DOWNLOADS_SOURCE_RESTORED=1' in generator
    assert 'rerunning release manifest generator after hydrating promoted downloads source from registry-backed artifacts' in generator
    assert 'exec env CHUMMER_RELEASE_MANIFEST_REHYDRATION_PASS=1 bash "$SCRIPT_DIR/generate-releases-manifest.sh"' in generator


def test_latest_nightly_publish_verifies_open_public_desktop_install_routes_after_public_edge_redeploy() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert 'PUBLIC_EDGE_VERIFY_BASE_URL="${CHUMMER_PUBLIC_EDGE_VERIFY_BASE_URL:-http://127.0.0.1:${CHUMMER_PUBLIC_EDGE_PORT:-8091}}"' in publisher
    assert 'PUBLIC_EDGE_VERIFY_HOST="${CHUMMER_PUBLIC_EDGE_VERIFY_HOST:-chummer.run}"' in publisher
    assert 'PUBLIC_EDGE_VERIFY_PROTO="${CHUMMER_PUBLIC_EDGE_VERIFY_PROTO:-https}"' in publisher
    assert "verify_public_edge_open_public_install_routes()" in publisher
    assert 'for key in ("downloads", "artifacts"):' in publisher
    assert 'install_access_class == "open_public"' in publisher
    assert 'expected_location = f"/downloads/get/{artifact_id}"' in publisher
    assert 'redirected back to login instead of direct public download' in publisher
    assert 'Published downloads shelf failed open-public installer route verification.' in publisher
    assert 'verify_public_edge_open_public_install_routes \\' in publisher
    assert 'docker compose -f docker-compose.public-edge.yml up -d' in publisher


def test_latest_nightly_publish_remains_preview_handoff_lane() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert 'PUBLIC_RELEASE_CHANNEL="${CHUMMER_PUBLIC_DEFAULT_RELEASE_CHANNEL:-preview}"' in publisher
    assert 'ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH="${CHUMMER_ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH:-0}"' in publisher
    assert "Nightly publisher is the preview handoff lane. Refusing stable/public_stable publication from this script." in publisher
    assert "is_publishable_nightly_stage()" in publisher
    assert 'if ! is_publishable_nightly_stage "$candidate"; then' in publisher
    assert "No publishable nightly stage found under $STAGING_ROOT" in publisher


def test_public_edge_e2e_enforces_direct_public_installer_handoff_routes() -> None:
    e2e = (REPO_ROOT / "scripts" / "e2e-public-edge.cjs").read_text(encoding="utf-8")

    assert "function publicInstallerRedirectMatches(response, artifactId)" in e2e
    assert "const expectedLocation = `/downloads/get/${artifactId}`;" in e2e
    assert "!decodeURIComponent(location).includes('/login?next=')" in e2e
    assert "payload.downloads.find(row => row?.artifactId === 'avalonia-win-x64-installer')" in e2e
    assert "payload.downloads.find(row => row?.artifactId === 'avalonia-linux-x64-installer')" in e2e
    assert "url: `${baseUrl}/downloads/install/avalonia-linux-x64-installer`," in e2e
    assert "url: `${baseUrl}/downloads/install/avalonia-win-x64-installer`," in e2e
    assert "publicInstallerRedirectMatches(response, 'avalonia-linux-x64-installer')" in e2e
    assert "publicInstallerRedirectMatches(response, 'avalonia-win-x64-installer')" in e2e


def test_portal_e2e_distinguishes_public_desktop_installer_handoffs_from_account_gated_routes() -> None:
    e2e = (REPO_ROOT / "scripts" / "e2e-portal.cjs").read_text(encoding="utf-8")

    assert "function expectsDirectPublicInstallRedirect(download)" in e2e
    assert "const expectedDirectDownloadRoute = `/downloads/get/${download.id}`;" in e2e
    assert "text.includes('data-download-action=\"download-artifact\"')" in e2e
    assert "text.includes('data-download-dispatch-url=')" in e2e
    assert "text.includes('data-download-link-mode=\"self-host-dispatch\"')" in e2e
    assert "installAccessClass === 'open_public'" in e2e
    assert "platform.includes('windows') || platform.includes('linux')" in e2e
    assert "kind === 'installer' || kind === 'msix' || kind === 'deb'" in e2e
    assert "decodedLocation === expectedDirectDownloadRoute || decodedLocation.endsWith(expectedDirectDownloadRoute)" in e2e
    assert "!decodedLocation.includes('/login?next=')" in e2e


def test_release_candidate_handoff_blocks_when_windows_smoke_exists_without_staged_artifact_or_manifest_row() -> None:
    handoff = (REPO_ROOT / "scripts" / "materialize_release_candidate_handoff.py").read_text(encoding="utf-8")
    handoff_doc = (REPO_ROOT / "docs" / "RELEASE_CANDIDATE_HANDOFF.md").read_text(encoding="utf-8")

    assert "Windows startup-smoke passed for" in handoff
    assert "staged installer bytes are missing" in handoff
    assert "does not expose a matching Windows artifact row" in handoff
    assert "windows_exit_gate_refresh" in handoff
    assert "maybe_materialize_windows_exit_gate" in handoff
    assert '"handoff_only": True' in handoff
    assert '"stable_release_unchanged": True' in handoff
    assert '"requires_separate_publish_lane": True' in handoff
    assert '"stage_proof_complete": stage_proof_complete' in handoff
    assert "Keep the live downloads shelf and stable channel unchanged" in handoff
    assert '"promotion_ready": stage_proof_complete' in handoff
    assert "This handoff does not publish the live downloads shelf and does not change the stable channel by itself." in handoff_doc
    assert "`stage_proof_complete: false`" in handoff_doc
    assert "Public/stable publication remains a separate explicit operator lane." in handoff_doc


def test_s3_publish_is_fail_closed_until_storage_has_atomic_immutable_cutover() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-download-bundle-s3.sh").read_text(encoding="utf-8")

    assert "Object-storage release publication is disabled fail-closed." in publisher
    assert "immutable, versioned artifact and proof object keys" in publisher
    assert "one atomic canonical pointer cutover" in publisher
    assert "scripts/publish-download-bundle-http.sh" in publisher
    assert "scripts/publish-download-bundle.sh" in publisher
    assert "exit 78" in publisher
    assert "\naws s3 " not in publisher
    assert "\npython3 " not in publisher


def test_windows_bootstrap_build_is_measured_by_the_real_payload_gate() -> None:
    builder = (REPO_ROOT / "scripts" / "build-desktop-installer.sh").read_text(encoding="utf-8")
    native_builder = (REPO_ROOT / "scripts" / "build-native-windows-bootstrap-installer.sh").read_text(encoding="utf-8")
    bootstrap_template = (REPO_ROOT / "scripts" / "windows-bootstrap" / "installer.nsi").read_text(encoding="utf-8")

    assert 'local installer_mode="${CHUMMER_WINDOWS_INSTALLER_MODE:-bootstrap}"' in builder
    assert 'bootstrap_payload_url="${CHUMMER_WINDOWS_BOOTSTRAP_PAYLOAD_URL:-${downloads_prefix%/}/$(basename "$payload_zip")}"' in builder
    assert 'write_windows_bootstrap_config' in builder
    assert 'scripts/build-native-windows-bootstrap-installer.sh' in builder
    assert 'verify_windows_installer_payload_gate "$DIST_DIR/$installer_name" "$DIST_DIR/files/$(basename "$payload_zip")"' in builder
    assert "Windows bootstrap installer build is blocked until the native bootstrap builder is wired." not in builder
    assert "The .NET WinForms installer is too large for bootstrap promotion" not in builder
    assert "Use CHUMMER_WINDOWS_INSTALLER_MODE=bundled for a local full installer" not in builder
    assert "bundled|append|appended)" in builder
    assert "7z2602-extra.7z" in native_builder
    assert "CHUMMER_WINDOWS_CURL_URL" in native_builder
    assert "CHUMMER_WINDOWS_CURL_SHA256" in native_builder
    assert 'mkdir -p "$STAGE_DIR/curl"' in native_builder
    assert "makensis" in native_builder
    assert 'ReadEnvStr $0 "TEMP"' in bootstrap_template
    assert 'ReadEnvStr $0 "TMP"' in bootstrap_template
    assert 'CreateDirectory "$0\\Chummer6"' in bootstrap_template
    assert 'Push "$0\\Chummer6\\installer-temp"' in bootstrap_template
    assert "InitPluginsDir" in bootstrap_template
    assert bootstrap_template.index('ReadEnvStr $0 "TEMP"') < bootstrap_template.index("InitPluginsDir")
    assert bootstrap_template.index("InitPluginsDir") < bootstrap_template.index('Push "$PLUGINSDIR"')
    assert "Function EnsureBootstrapTempRoot" in bootstrap_template
    assert "Function NormalizePathToR9" in bootstrap_template
    assert "Function TryUseBootstrapTempRootCandidate" in bootstrap_template
    assert 'GetFullPathName $1 "$0"' in bootstrap_template
    assert 'FileOpen $2 "$9\\bootstrap-root-probe.tmp" w' in bootstrap_template
    assert 'Push "Bootstrap temp root: $BootstrapTempRoot"' in bootstrap_template
    assert 'SetOutPath "$BootstrapTempRoot"' in bootstrap_template
    assert 'File /oname=7za.exe "${CHUMMER_STAGE_DIR}/7zip/7za.exe"' in bootstrap_template
    assert 'File /oname=curl.exe "${CHUMMER_STAGE_DIR}/curl/curl.exe"' in bootstrap_template
    assert 'File /oname=libcurl-x64.dll "${CHUMMER_STAGE_DIR}/curl/libcurl-x64.dll"' in bootstrap_template
    assert 'File /oname=curl-ca-bundle.crt "${CHUMMER_STAGE_DIR}/curl/curl-ca-bundle.crt"' in bootstrap_template
    assert 'Push "$BootstrapTempRoot\\${CHUMMER_PAYLOAD_FILE_NAME}"' in bootstrap_template
    assert "Call NormalizePathToR9" in bootstrap_template
    assert 'StrCpy $EffectivePayloadPath $9' in bootstrap_template
    assert 'StrCpy $1 $EffectivePayloadPath 2' in bootstrap_template
    assert 'Push "Chummer could not resolve a writable payload download target."' in bootstrap_template
    assert 'Push "Payload download target: $EffectivePayloadPath"' in bootstrap_template
    assert "Function TryDownloadPayloadWithCurl" in bootstrap_template
    assert "Var DownloadHelperPartialPath" in bootstrap_template
    assert "Var DownloadHelperExitCodePath" in bootstrap_template
    assert "Function UpdateInstFilesStatusText" in bootstrap_template
    assert "Function SetInstFilesProgressPosition" in bootstrap_template
    assert 'GetDlgItem $1 $HWNDPARENT 1006' in bootstrap_template
    assert 'GetDlgItem $1 $HWNDPARENT 0x3ec' in bootstrap_template
    assert 'StrCpy $DownloadHelperPartialPath "$BootstrapTempRoot\\${CHUMMER_PAYLOAD_FILE_NAME}.partial"' in bootstrap_template
    assert 'StrCpy $DownloadHelperStartedPath "$BootstrapTempRoot\\download-started.txt"' in bootstrap_template
    assert 'StrCpy $DownloadHelperExitCodePath "$BootstrapTempRoot\\download-exit-code.txt"' in bootstrap_template
    assert 'StrCpy $DownloadHelperStdErrPath "$BootstrapTempRoot\\download-curl-stderr.txt"' in bootstrap_template
    assert 'FileWrite $6 ">$\\"$DownloadHelperStartedPath$\\" echo started$\\r$\\n"' in bootstrap_template
    assert 'FileWrite $6 "del /q $\\"$DownloadHelperPartialPath$\\" 2>nul$\\r$\\n"' in bootstrap_template
    assert 'FileWrite $6 "del /q $\\"$EffectivePayloadPath$\\" 2>nul$\\r$\\n"' in bootstrap_template
    assert 'FileWrite $6 "$\\"$BootstrapTempRoot\\curl.exe$\\" --location --fail --silent --show-error --retry 5 --retry-delay 2 --connect-timeout 20 --cacert $\\"$BootstrapTempRoot\\curl-ca-bundle.crt$\\" --output $\\"$DownloadHelperPartialPath$\\" $\\"$EffectivePayloadUrl$\\" 1>$\\"$BootstrapTempRoot\\download-curl-stdout.txt$\\" 2>$\\"$DownloadHelperStdErrPath$\\"$\\r$\\n"' in bootstrap_template
    assert 'FileWrite $6 ">$\\"$DownloadHelperExitCodePath$\\" echo %EXITCODE%$\\r$\\n"' in bootstrap_template
    assert 'nsExec::ExecToStack \'"$SYSDIR\\cmd.exe" /C start "" /B "$SYSDIR\\cmd.exe" /C call $6\'' in bootstrap_template
    assert 'StrCpy $0 "Downloading application files - $6% - $3 / $8 MiB - $2"' in bootstrap_template
    assert 'StrCpy $0 "Downloading application files - 100% - $3 / $8 MiB - $2"' in bootstrap_template
    assert 'StrCpy $DownloadHelperOutput "bundled curl downloader did not start."' in bootstrap_template
    assert 'StrCpy $DownloadHelperOutput "bundled curl download timed out."' in bootstrap_template
    assert 'Push "Payload download completed with bundled curl"' in bootstrap_template
    assert 'Push "Bundled curl download failed code=$DownloadHelperStatus output=$DownloadHelperOutput"' in bootstrap_template
    assert 'Push "Payload download failed; legacy NSIS downloader is disabled for bootstrap installs"' in bootstrap_template
    assert "NSISdl::download" not in bootstrap_template
    assert 'Delete "$BootstrapTempRoot\\chummer-verify-size.cmd"' in bootstrap_template
    assert 'FileOpen $6 "$BootstrapTempRoot\\chummer-verify-size.cmd" w' in bootstrap_template
    assert 'FileWrite $6 "for %%I in ($\\"$EffectivePayloadPath$\\") do @echo %%~zI$\\r$\\n"' in bootstrap_template
    assert 'GetFullPathName /SHORT $7 "$BootstrapTempRoot\\chummer-verify-size.cmd"' in bootstrap_template
    assert 'Delete "$BootstrapTempRoot\\payload-hash.txt"' in bootstrap_template
    assert 'FileOpen $6 "$BootstrapTempRoot\\chummer-verify-payload.cmd" w' in bootstrap_template
    assert 'FileWrite $6 "7za.exe h -scrcSHA256 $\\"$EffectivePayloadPath$\\" > payload-hash.txt$\\r$\\n"' in bootstrap_template
    assert 'GetFullPathName /SHORT $7 "$BootstrapTempRoot\\chummer-verify-payload.cmd"' in bootstrap_template
    assert 'nsExec::ExecToStack \'"$SYSDIR\\cmd.exe" /C call $6\'' in bootstrap_template
    assert 'FileOpen $3 "$BootstrapTempRoot\\payload-hash.txt" r' in bootstrap_template
    assert 'FileOpen $6 "$BootstrapTempRoot\\chummer-extract-payload.cmd" w' in bootstrap_template
    assert 'FileWrite $6 "7za.exe x -y $\\"-o$INSTDIR$\\" $\\"$EffectivePayloadPath$\\"$\\r$\\n"' in bootstrap_template
    assert 'GetFullPathName /SHORT $7 "$BootstrapTempRoot\\chummer-extract-payload.cmd"' in bootstrap_template
    assert bootstrap_template.count('nsExec::ExecToStack \'"$SYSDIR\\cmd.exe" /C call $6\'') >= 2
    assert 'WriteRegStr HKCU "Software\\Classes\\chummer\\shell\\open\\command"' in bootstrap_template
    assert 'pending-claim-code.txt' in bootstrap_template
    assert 'cp -f "$DIST_DIR/$installer_name" "$DIST_DIR/files/$installer_name"' in builder
    assert builder.index('cp -f "$DIST_DIR/$installer_name" "$DIST_DIR/files/$installer_name"') < builder.index('verify_windows_installer_payload_gate "$DIST_DIR/$installer_name" "$DIST_DIR/files/$(basename "$payload_zip")"')


def test_unsigned_public_release_override_disables_packaging_signing_requirements() -> None:
    result = subprocess.run(
        [
            "bash",
            str(REPO_ROOT / "scripts" / "resolve-desktop-release-context.sh"),
        ],
        text=True,
        capture_output=True,
        check=False,
        env={
            "CHUMMER_DESKTOP_RELEASE_CHANNEL": "public_stable",
            "CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE": "true",
        },
    )

    assert result.returncode == 0, result.stderr
    assert "public_release=true" in result.stdout
    assert "allow_unsigned_public_release=true" in result.stdout
    assert "windows_signing_required=false" in result.stdout
    assert "mac_signing_required=false" in result.stdout
    assert "mac_notarization_required=false" in result.stdout


def test_windows_startup_smoke_prefers_local_bootstrap_payload_sidecar_when_present() -> None:
    smoke = (REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh").read_text(encoding="utf-8")

    assert 'chummerwinsmokeXXXXXX' in smoke
    assert 'local payload_name="${artifact_name%-installer.exe}-payload.zip"' in smoke
    assert 'local_payload_path="$artifact_dir/files/$payload_name"' in smoke
    assert "WINDOWS_LOCAL_PAYLOAD_COPY" in smoke
    assert "winepath -u 'C:\\\\windows\\\\temp'" in smoke
    assert 'cp "$local_payload_path" "$WINDOWS_LOCAL_PAYLOAD_COPY"' in smoke
    assert 'CHUMMER_INSTALLER_PAYLOAD_PATH="$(to_native_path "$local_payload_path")"' in smoke
    assert 'CHUMMER_INSTALLER_PAYLOAD_SHA256="$local_payload_sha256"' in smoke
    assert 'CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES="$local_payload_size_bytes"' in smoke


def test_startup_smoke_fails_closed_for_a_requested_mouse_journey_or_tester_trace() -> None:
    smoke = (REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh").read_text(encoding="utf-8")

    assert "requested_mouse_first_journey_passes()" in smoke
    assert 'if [[ "$mouse_status" != "pass" && "$mouse_status" != "passed" ]]' in smoke
    assert 'local user_journey_trace_path="${CHUMMER_DESKTOP_USER_JOURNEY_TRACE_OUTPUT:-}"' in smoke
    assert "Requested user-journey tester trace was not emitted" in smoke
    assert 'mouse_first_journey_validation_failed=1' in smoke
    assert '[[ "$status" -ne 0 && "$mouse_first_journey_validation_failed" -eq 0 ]]' in smoke


def test_startup_smoke_receipts_disclose_only_portable_process_file_names() -> None:
    smoke = (REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh").read_text(encoding="utf-8")

    assert smoke.count('payload["processPath"] = process_file_name or "<redacted:process-path>"') >= 2
    assert smoke.count('payload["processPathDisclosure"] = "file_name_only"') >= 2
    assert '"processPath": portable_process_path,' in smoke
    assert '"processPathDisclosure": "file_name_only" if portable_process_path else "unavailable",' in smoke
    assert '"artifactPath": artifact_relative_path,' in smoke
    assert '"artifactPathDisclosure": artifact_path_disclosure,' in smoke
    assert '"startupReceiptPath": startup_receipt_name,' in smoke
    assert '"startupReceiptPathDisclosure": "file_name_only",' in smoke
    assert 'payload["artifactPath"] = artifact_relative_path' in smoke
    assert 'payload["artifactPathDisclosure"] = artifact_path_disclosure' in smoke
    assert smoke.count('artifact_shelf_token = artifact_parent_name if artifact_parent_name in {"files"} else ""') >= 4
    assert 'raw_tail_text = "\\n".join(raw_tail_lines)' in smoke
    assert 'tail_lines = [redact_user_profile_paths(line) for line in raw_tail_lines]' in smoke
    assert '"logTailRedaction": "known_user_profile_paths",' in smoke


def test_release_manifest_generation_prunes_install_proof_routes_to_published_artifacts() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "prune_release_proof_routes_to_manifest_artifacts" in generator
    assert 'route.startswith("/downloads/install/")' in generator
    assert 'artifact_id in artifact_ids' in generator
    assert 'release_proof["proofRoutes"] = prune_routes' in generator


def test_release_manifest_generation_can_skip_external_host_proof_blockers_for_artifact_only_publish_paths() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert 'RUN_SERVICES_DOWNLOADS_ROOT="${RUN_SERVICES_DOWNLOADS_ROOT:-$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads}"' in generator
    assert 'GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS="${CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS:-1}"' in generator
    assert 'if to_bool "$GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS"; then' in generator
    assert 'run_services_canonical_manifest_path="$RUN_SERVICES_DOWNLOADS_ROOT/RELEASE_CHANNEL.generated.json"' in generator
    assert 'external_host_proof_manifest_path="$run_services_canonical_manifest_path"' in generator
    assert 'materialize-external-host-proof-blockers.py' in generator
    assert 'echo "skipped external host proof blocker materialization"' in generator


def test_publish_download_bundle_defaults_external_host_proof_blockers_off_during_shelf_sync() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert 'CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS="${CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS:-0}" \\' in publish_script


def test_publish_download_bundle_only_auto_syncs_live_mirrors_for_live_deploy_roots() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert "deploy_dir_is_live_downloads_root()" in publish_script
    assert 'if ! deploy_dir_is_live_downloads_root "$deploy_dir_physical"; then' in publish_script
    assert 'configured="${CHUMMER_PUBLIC_EDGE_DOWNLOADS_MIRROR_DIRS:-}"' in publish_script
    assert '"$REPO_ROOT/Chummer.Portal/downloads" \\' in publish_script
    assert '"$REPO_ROOT/.codex-studio/published/portal" \\' in publish_script
    assert '"$REPO_ROOT/../chummer-presentation/Chummer.Portal/downloads" \\' in publish_script
    assert '"$REPO_ROOT/../chummer-presentation/.codex-studio/published/portal" \\' in publish_script
    assert '"$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads" \\' in publish_script
    assert publish_script.index('if ! deploy_dir_is_live_downloads_root "$deploy_dir_physical"; then') < publish_script.index('if [[ "$deploy_dir_physical" != "$canonical_downloads_physical" ]]; then')


def test_publish_download_bundle_carries_windows_bootstrap_progress_logs_into_the_deploy_shelf() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert "refresh_release_build_handoff()" in publish_script
    assert 'refresh_release_build_handoff "$BUNDLE_DIR"' in publish_script
    assert 'refresh_release_build_handoff "$DEPLOY_DIR"' in publish_script
    assert '-name "windows-installer-progress-*.log"' in publish_script
    assert 'cp -f "$STARTUP_SMOKE_SOURCE"/windows-installer-progress-*.log "$startup_smoke_deploy_dir"/' in publish_script
    assert 'bash "$SCRIPT_DIR/generate-releases-manifest.sh"' in publish_script
    assert 'python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py" \\' in publish_script
    assert "verify_windows_desktop_exit_gate()" in publish_script
    assert 'bash "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >/dev/null' in publish_script
    assert 'CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH="$DEPLOY_DIR/RELEASE_CHANNEL.generated.json"' in publish_script
    assert 'CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$DEPLOY_DIR/files"' in publish_script
    assert 'CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="$visual_proof_path"' in publish_script
    assert 'CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH="$gate_output"' in publish_script
    assert "emit_windows_visual_proof_handoff_guidance()" in publish_script
    assert 'emit_windows_visual_proof_handoff_guidance "$BUNDLE_DIR" "$DEPLOY_DIR"' in publish_script
    assert "Windows visual proof handoff:" in publish_script
    assert "Windows visual proof summary:" in publish_script
    assert "Published downloads shelf failed Windows desktop exit gate verification. Use the Windows visual proof handoff above." in publish_script
    assert '--release-channel "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \\' in publish_script
    assert '--downloads-manifest "$DEPLOY_DIR/releases.json" \\' in publish_script
    assert '--startup-smoke-dir "$STARTUP_SMOKE_SOURCE" \\' in publish_script
    assert '--files-dir "$DEPLOY_DIR/files" >/dev/null' in publish_script
    assert publish_script.index('python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py" \\') < publish_script.rindex("\nverify_windows_desktop_exit_gate\n")


def test_public_stable_publish_download_bundle_requires_root_release_truth_clearance() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert 'ROOT_RELEASE_BLOCKERS_PATH="${CHUMMER_ROOT_RELEASE_BLOCKERS_PATH:-$REPO_ROOT/../RELEASE_BLOCKERS.generated.json}"' in publish_script
    assert 'PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS="${CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS:-86400}"' in publish_script
    assert "require_public_stable_root_blocker_clearance()" in publish_script
    assert 'if [[ "$normalized_release_channel" != "public_stable" ]]; then' in publish_script
    assert 'python3 - "$ROOT_RELEASE_BLOCKERS_PATH" "$PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS"' in publish_script
    assert "Public stable publication requires fresh root release blocker truth." in publish_script
    assert '"release_posture:non_flagship_channel"' in publish_script
    assert 'require_public_stable_root_blocker_clearance "$release_channel"' in publish_script
    assert publish_script.index('require_public_stable_root_blocker_clearance "$release_channel"') < publish_script.index('bash "$SCRIPT_DIR/generate-releases-manifest.sh"')


def test_release_build_checks_are_owned_by_local_scripts() -> None:
    assert (REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh").is_file()
    assert (REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh").is_file()
    assert (REPO_ROOT / "scripts" / "materialize_release_candidate_handoff.py").is_file()


def test_linux_desktop_exit_gate_reports_direct_host_build_failures_before_missing_host_noise() -> None:
    gate = (REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh").read_text(encoding="utf-8")

    assert 'RUN_SERVICES_RELEASE_CHANNEL_PATH="${CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH:-/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json}"' in gate
    assert 'DEFAULT_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"' in gate
    assert 'RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"' in gate
    assert 'LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_LINUX_DESKTOP_EXIT_GATE_LOCAL_DESKTOP_FILES_ROOT"' in gate
    assert 'LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"' in gate
    assert 'local test_output_root="$test_project_dir/bin/Release"' in gate
    assert 'local test_assembly_path="$test_project_dir/bin/Release/$FRAMEWORK/$TEST_ASSEMBLY_NAME"' in gate
    assert 'find "$test_output_root" -maxdepth 4 -type f -name "${TEST_ASSEMBLY_NAME%.dll}"' in gate
    assert 'find "$test_output_root" -maxdepth 4 -type f -name "$TEST_ASSEMBLY_NAME"' in gate
    assert 'KEEP_SOURCE_SNAPSHOT="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_KEEP_SOURCE_SNAPSHOT:-0}"' in gate
    assert '[linux-desktop-exit-gate] desktop runtime test host build failed' in gate
    assert 'desktop runtime test host via dotnet' in gate
    assert 'exec dotnet "$(basename "$test_assembly_path")" "$@"' in gate
    assert 'Promoted Linux installer file is missing from the release-aligned desktop shelf' in gate
    assert gate.index('desktop runtime test host build failed') < gate.index('desktop runtime test host is missing or not executable')


def test_windows_and_macos_desktop_exit_gates_prefer_live_run_services_release_channel_defaults() -> None:
    windows_gate = (REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh").read_text(encoding="utf-8")
    macos_gate = (REPO_ROOT / "scripts" / "materialize-macos-desktop-exit-gate.sh").read_text(encoding="utf-8")

    expected = 'RUN_SERVICES_RELEASE_CHANNEL_PATH="${CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH:-/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json}"'
    assert expected in windows_gate
    assert expected in macos_gate
    assert 'if [[ -f "$RUN_SERVICES_RELEASE_CHANNEL_PATH" && ( ! -f "$RELEASE_CHANNEL_PATH_DEFAULT" || "$RUN_SERVICES_RELEASE_CHANNEL_PATH" -nt "$RELEASE_CHANNEL_PATH_DEFAULT" ) ]]; then' in windows_gate
    assert 'if [[ -f "$RUN_SERVICES_RELEASE_CHANNEL_PATH" && ( ! -f "$RELEASE_CHANNEL_PATH_DEFAULT" || "$RUN_SERVICES_RELEASE_CHANNEL_PATH" -nt "$RELEASE_CHANNEL_PATH_DEFAULT" ) ]]; then' in macos_gate


def test_visual_workflow_and_flagship_ui_gates_prefer_live_run_services_release_channel_defaults() -> None:
    visual_gate = (REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-visual-familiarity-exit-gate.sh").read_text(encoding="utf-8")
    workflow_gate = (REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-workflow-execution-gate.sh").read_text(encoding="utf-8")
    flagship_gate = (REPO_ROOT / "scripts" / "ai" / "milestones" / "b14-flagship-ui-release-gate.sh").read_text(encoding="utf-8")

    expected = 'run_services_release_channel_path="${CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH:-/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json}"'
    assert expected in visual_gate
    assert expected in workflow_gate
    assert expected in flagship_gate
    assert 'release_channel_path_default="$run_services_release_channel_path"' in visual_gate
    assert 'release_channel_path_default="$run_services_release_channel_path"' in workflow_gate
    assert 'release_channel_path_default="$run_services_release_channel_path"' in flagship_gate


def test_workflow_gate_keeps_sr4_sr6_channel_alignment_fail_closed_when_human_side_authority_is_present() -> None:
    workflow_gate = (REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-workflow-execution-gate.sh").read_text(encoding="utf-8")

    assert 'label in {"sr4_workflow_parity", "sr6_workflow_parity"}' in workflow_gate
    assert 'human_side_rule_authority_is_approved' in workflow_gate
    assert 'evidence["human_side_rule_authority_execution_waiver_enabled"] = False' in workflow_gate
    assert "channel_alignment_recovered_from_human_side_rule_authority" not in workflow_gate
    assert "or human_side_rule_authority_is_approved" not in workflow_gate


def test_windows_desktop_exit_gate_prefers_release_aligned_shelf_before_repo_fallback() -> None:
    gate = (REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh").read_text(encoding="utf-8")

    assert 'DEFAULT_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"' in gate
    assert 'RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"' in gate
    assert 'WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT"' in gate
    assert 'WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"' in gate
    assert "Promoted Windows installer was not resolved from the release-aligned desktop shelf." in gate


def test_macos_desktop_exit_gate_prefers_release_aligned_shelf_before_repo_fallback() -> None:
    gate = (REPO_ROOT / "scripts" / "materialize-macos-desktop-exit-gate.sh").read_text(encoding="utf-8")

    assert "upper_ascii()" in gate
    assert 'DEFAULT_MACOS_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"' in gate
    assert 'RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"' in gate
    assert 'MACOS_LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_MACOS_LOCAL_DESKTOP_FILES_ROOT"' in gate
    assert 'MACOS_LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"' in gate
    assert "Promoted macOS installer was not resolved from the release-aligned desktop shelf" in gate
    assert '${APP_KEY^^}' not in gate
    assert '${RID^^}' not in gate


def test_aggregate_desktop_materializer_defers_to_release_aligned_shelf_resolution() -> None:
    gate = (REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-executable-exit-gate.sh").read_text(encoding="utf-8")

    assert "upper_ascii()" in gate
    assert 'CHUMMER_LINUX_DESKTOP_EXIT_GATE_LOCAL_DESKTOP_FILES_ROOT="${hub_published_files_root:-}"' in gate
    assert 'CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="${hub_published_files_root:-}"' in gate
    assert 'CHUMMER_MACOS_LOCAL_DESKTOP_FILES_ROOT="${hub_published_files_root:-}"' in gate
    assert 'release_channel_path_value = globals().get("release_channel_path")' in gate
    assert 'release_channel_root = (' in gate
    assert 'release_aligned_files_root = release_channel_root / "files"' in gate
    assert 'release_aligned_startup_smoke_root = release_channel_root / "startup-smoke"' in gate
    assert 'installer_path = str(release_aligned_files_root / installer_name)' in gate
    assert 'mkdir -p {release_aligned_files_root}' in gate
    assert 'installer_path_suffix = f"/files/{installer_name}"' in gate
    assert '${head^^}' not in gate
    assert '${rid^^}' not in gate
    assert 'startup_smoke_suffix = "/startup-smoke"' in gate


def test_next90_m144_guard_prefers_release_aligned_shelf_before_repo_fallback() -> None:
    gate = (
        REPO_ROOT
        / "scripts"
        / "ai"
        / "milestones"
        / "next90-m144-ui-startup-smoke-and-executable-gate-check.sh"
    ).read_text(encoding="utf-8")

    assert 'default_downloads_root="$repo_root/Docker/Downloads/files"' in gate
    assert 'default_startup_smoke_dir="$repo_root/Docker/Downloads/startup-smoke"' in gate
    assert 'release_channel_directory="$(cd "$(dirname "$release_channel_path")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'release_aligned_downloads_root="$release_channel_directory/files"' in gate
    assert 'release_aligned_startup_smoke_dir="$release_channel_directory/startup-smoke"' in gate
    assert 'downloads_root="$CHUMMER_NEXT90_M144_DOWNLOADS_ROOT"' in gate
    assert 'downloads_root="$release_aligned_downloads_root"' in gate
    assert 'startup_smoke_dir="$CHUMMER_NEXT90_M144_STARTUP_SMOKE_DIR"' in gate
    assert 'startup_smoke_dir="$release_aligned_startup_smoke_dir"' in gate
    assert "is missing a local artifact under the release-aligned desktop shelf." in gate
