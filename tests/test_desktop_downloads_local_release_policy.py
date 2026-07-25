from __future__ import annotations

import json
import os
import re
import subprocess
import tarfile
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
AI_DAY1_SETUP = REPO_ROOT / "scripts" / "ai" / "day1-p1-setup.sh"
AI_CODEX_WRAPPERS = (
    REPO_ROOT / "scripts" / "ai" / "run_codex.sh",
    REPO_ROOT / "scripts" / "ai" / "run_codex_resume.sh",
)
AI_DAY1_WRAPPERS = (
    REPO_ROOT / "scripts" / "ai" / "day1-clean-artifacts.sh",
    REPO_ROOT / "scripts" / "ai" / "day1-all-milestones.sh",
    REPO_ROOT / "scripts" / "ai" / "day1-p1-run.sh",
    REPO_ROOT / "scripts" / "ai" / "day1-p1-loop.sh",
)
AI_SHARED_ENV_UTILITY_WRAPPERS = (
    REPO_ROOT / "scripts" / "ai" / "clean.sh",
    REPO_ROOT / "scripts" / "ai" / "format.sh",
    REPO_ROOT / "scripts" / "ai" / "test-matrix.sh",
    REPO_ROOT / "scripts" / "ai" / "coverage.sh",
)
ARRAY_COUNT_HELPER_SNIPPETS = (
    "array_count()",
    'local restore_nounset=0',
    'case "$-" in',
    'set +u',
    'eval "set -- \\"\\${${array_name}[@]}\\""',
    'local count="$#"',
    'set -u',
)
RELEASE_ARRAY_PORTABILITY_EXPECTATIONS = {
    REPO_ROOT / "scripts" / "generate-releases-manifest.sh": {
        "required": (
            'promoted_file_count="$(array_count promoted_file_names)"',
            'if (( promoted_file_count == 0 )); then',
            'portal_artifact_count="$(array_count portal_artifacts)"',
            'if (( portal_artifact_count > 0 )); then',
            'echo "synced ${portal_artifact_count} local portal artifact(s) -> $target_dir"',
            'echo "synced ${portal_artifact_count} ${target_label} artifact(s) -> $target_dir"',
        ),
        "forbidden": (
            '${#promoted_file_names[@]}',
            '${#portal_artifacts[@]}',
        ),
    },
    REPO_ROOT / "scripts" / "publish-download-bundle.sh": {
        "required": (
            'installer_candidate_count="$(array_count installer_candidates)"',
            'if (( installer_candidate_count == 0 )); then',
            'artifact_count="$(array_count artifacts)"',
            'if (( artifact_count == 0 )); then',
            'live_downloads_mirror_dir_count="$(array_count live_downloads_mirror_dirs)"',
            'if [[ "$WINDOWS_ONLY_PUBLICATION_MODE" != "true" ]] && (( live_downloads_mirror_dir_count > 0 )); then',
            'promoted_file_count="$(array_count promoted_file_names)"',
            '--promoted-artifact-count "$promoted_file_count"',
            'echo "synced ${promoted_file_count} promoted artifact(s) -> $target_label mirror $target_dir"',
            'echo "Published ${promoted_file_count} desktop artifact(s) through verified external downloads lane: $LIVE_VERIFY_TARGET"',
            'echo "Updated local downloads shelf with ${promoted_file_count} desktop artifact(s): $DEPLOY_DIR"',
        ),
        "forbidden": (
            '${#installer_candidates[@]}',
            '${#artifacts[@]}',
            '${#live_downloads_mirror_dirs[@]}',
            '${#promoted_file_names[@]}',
        ),
    },
    REPO_ROOT / "scripts" / "build-desktop-installer.sh": {
        "required": (
            'artifact_count="$(array_count artifacts)"',
            'if (( artifact_count == 0 )); then',
        ),
        "forbidden": (
            '${#artifacts[@]}',
        ),
    },
    REPO_ROOT / "scripts" / "publish-download-bundle-s3.sh": {
        "required": (
            'windows_payload_gate_args_count="$(array_count windows_payload_gate_args)"',
            'if (( windows_payload_gate_args_count == 6 )); then',
        ),
        "forbidden": (
            '${#windows_payload_gate_args[@]}',
        ),
    },
    REPO_ROOT / "scripts" / "publish-download-bundle-http.sh": {
        "required": (
            "array_values_nul()",
            'windows_payload_gate_args_count="$(array_count windows_payload_gate_args)"',
            'if (( windows_payload_gate_args_count == 8 )); then',
            'upload_file_count="$(array_count upload_files)"',
            'if (( upload_file_count == 0 )); then',
            'echo "Publishing ${upload_file_count} bundle files from $BUNDLE_DIR"',
            'done < <(array_values_nul upload_files)',
        ),
        "forbidden": (
            '${#windows_payload_gate_args[@]}',
            '${#upload_files[@]}',
            'for file_path in "${upload_files[@]}"; do',
        ),
    },
    REPO_ROOT / "scripts" / "verify-releases-manifest.sh": {
        "required": (
            'verify_arg_count="$(array_count VERIFY_ARGS)"',
            'if (( verify_arg_count > 0 )); then',
        ),
        "forbidden": (
            '${#VERIFY_ARGS[@]}',
        ),
    },
}
RUNBOOK_ALIAS_SAFE_SCRIPTS = (
    REPO_ROOT / "scripts" / "runbook.sh",
    REPO_ROOT / "scripts" / "runbook-strict-host-gates.sh",
    REPO_ROOT / "scripts" / "check-host-gate-prereqs.sh",
    REPO_ROOT / "scripts" / "validate-amend-manifests.sh",
    REPO_ROOT / "scripts" / "generate-parity-checklist.sh",
)
RELEASE_SUPPORT_ALIAS_SAFE_SCRIPTS = (
    REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh",
    REPO_ROOT / "scripts" / "resolve-hub-registry-root.sh",
    REPO_ROOT / "scripts" / "preflight-macos-packaging.sh",
)


def assert_release_script_uses_alias_safe_repo_root(script_path: Path) -> None:
    text = script_path.read_text(encoding="utf-8")

    assert 'SCRIPT_DIR_PHYSICAL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"' in text
    assert 'REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR_PHYSICAL/.." && pwd -P)"' in text
    assert 'REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"' in text
    assert 'REPO_ROOT="$REPO_ROOT_PHYSICAL"' in text
    assert 'SCRIPT_DIR="$REPO_ROOT/scripts"' in text
    assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"' not in text
    assert 'REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"' not in text


def test_github_actions_workflows_are_an_exact_read_only_ci_and_evidence_allowlist() -> None:
    workflows_root = REPO_ROOT / ".github" / ("work" + "flows")
    expected = {
        "global-flagship-release-approval.yml",
        "linux-native-lifecycle-evidence.yml",
        "linux-native-candidate-export.yml",
        "macos-flagship-evidence.yml",
        "macos-hosted-capacity-probe.yml",
        "pull-request-ci.yml",
        "preview-nightly-candidate-export.yml",
        "unsigned-windows-preview-nightly-candidate-export.yml",
        "windows-native-evidence-capture.yml",
        "windows-native-evidence-finalize.yml",
    }

    assert workflows_root.is_dir()
    assert {entry.name for entry in workflows_root.iterdir()} == expected

    forbidden_release_capabilities = (
        "contents: write",
        "packages: write",
        "id-token: write",
        "pull-requests: write",
        "issues: write",
        "deploy-pages",
        "createRelease",
        "uploadReleaseAsset",
        "gh release",
        "publish-latest-nightly-to-downloads",
        "publish-download-bundle",
    )
    macos_evidence_secrets = {
        "CHUMMER_MACOS_DEVELOPER_ID_P12_BASE64",
        "CHUMMER_MACOS_DEVELOPER_ID_P12_PASSWORD",
        "CHUMMER_MACOS_NOTARY_ISSUER_ID",
        "CHUMMER_MACOS_NOTARY_KEY_ID",
        "CHUMMER_MACOS_NOTARY_KEY_P8_BASE64",
    }
    for workflow_name in sorted(expected):
        workflow = (workflows_root / workflow_name).read_text(encoding="utf-8")
        secret_references = re.findall(
            r"\$\{\{\s*secrets\.([A-Za-z0-9_]+)\s*\}\}", workflow
        )
        assert workflow.count("secrets.") == len(secret_references)
        assert "secrets[" not in workflow
        if workflow_name == "macos-flagship-evidence.yml":
            assert set(secret_references) == macos_evidence_secrets
            assert "environment: macos-flagship-evidence" in workflow
        else:
            assert secret_references == []
            assert "secrets." not in workflow
        for capability in forbidden_release_capabilities:
            assert capability not in workflow
        for line in workflow.splitlines():
            action = line.strip()
            if not action.startswith("uses:"):
                continue
            assert re.fullmatch(r"uses: [A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}", action)

    action_dispatchers = {
        "linux-native-candidate-export.yml",
        "preview-nightly-candidate-export.yml",
    }
    for workflow_name in action_dispatchers:
        exporter = (workflows_root / workflow_name).read_text(encoding="utf-8")
        assert exporter.count("actions: write") == 1
    for workflow_name in expected - action_dispatchers:
        assert "actions: write" not in (workflows_root / workflow_name).read_text(
            encoding="utf-8"
        )

    approval = (
        workflows_root / "global-flagship-release-approval.yml"
    ).read_text(encoding="utf-8")
    assert "# This workflow is deliberately non-publishing." in approval
    assert "permissions:\n  actions: read\n  contents: read" in approval
    assert "Upload only the immutable approval receipt" in approval

    capacity_probe = (
        workflows_root / "macos-hosted-capacity-probe.yml"
    ).read_text(encoding="utf-8")
    assert "# This workflow never receives an environment" in capacity_probe
    assert "permissions:\n  contents: read" in capacity_probe
    assert "\nenvironment:" not in capacity_probe
    assert "Upload the nonsecret probe receipt" in capacity_probe


def test_pull_request_ci_runs_exact_stage_scope_against_pinned_registry_authority() -> None:
    workflow = (REPO_ROOT / ".github" / "workflows" / "pull-request-ci.yml").read_text(
        encoding="utf-8"
    )
    registry_commit = "51145559a4b3b95b5901c391edf3f17fd6714227"

    assert "repository: ArchonMegalon/chummer6-hub-registry" in workflow
    assert f"ref: {registry_commit}" in workflow
    assert f'= "{registry_commit}"' in workflow
    assert "CHUMMER_UI_TEST_REGISTRY_ROOT:" in workflow
    assert "tests/test_global_flagship_release_assembler.py" in workflow
    assert "tests/test_desktop_native_lifecycle_evidence.py" in workflow
    assert "tests/test_macos_flagship_evidence.py" in workflow
    assert "tests/test_preview_nightly_stage_contract.py" in workflow
    assert "tests/test_desktop_downloads_local_release_policy.py" in workflow


def test_daily_publish_policy_is_documented_in_local_runbook() -> None:
    runbook = (REPO_ROOT / "docs" / "SELF_HOSTED_DOWNLOADS_RUNBOOK.md").read_text(encoding="utf-8")

    assert "RUNBOOK_MODE=publish-latest-nightly" in runbook
    assert "08:00 Europe/Vienna" in runbook
    assert "once per day in the morning release window" in runbook
    assert "Build only what the proof needs" in runbook
    assert "does not publish the live downloads shelf and does not change the stable channel by itself" in runbook
    assert "force does not bypass installer eligibility or release proof gates" in runbook
    assert "CHUMMER_NIGHTLY_SUPPORT_PROOF_ONLY_HANDOFF=1" in runbook
    assert "A macOS-only, account-gated, hidden, quarantined, or support-only artifact set cannot replace the downloadable shelf." in runbook
    assert ("workflow" + "_dispatch") not in runbook
    assert ("GitHub " + "Actions") not in runbook


def test_runbook_desktop_build_can_wrap_installer_packaging() -> None:
    runbook = (REPO_ROOT / "scripts" / "runbook.sh").read_text(encoding="utf-8")

    assert 'DESKTOP_BUILD_PACKAGE="${DESKTOP_BUILD_PACKAGE:-0}"' in runbook
    assert 'desktop_build_package_requested=0' in runbook
    assert 'elif [[ -n "$DESKTOP_PUBLISH_DIR" || -n "$DESKTOP_APP_KEY" || -n "$DESKTOP_RID" || -n "$DESKTOP_LAUNCH_TARGET" ]]; then' in runbook
    assert "desktop-build packaging mode requires DESKTOP_PUBLISH_DIR, DESKTOP_APP_KEY, DESKTOP_RID, and DESKTOP_LAUNCH_TARGET." in runbook
    assert "Use scripts/build-desktop-installer.sh through this wrapper by setting DESKTOP_BUILD_PACKAGE=1 and the required packaging inputs." in runbook
    assert "Use the project build path when you only need a compile." in runbook
    assert "bash scripts/build-desktop-installer.sh \\" in runbook
    assert '"$DESKTOP_PUBLISH_DIR"' in runbook
    assert '"$DESKTOP_APP_KEY"' in runbook
    assert '"$DESKTOP_RID"' in runbook
    assert '"$DESKTOP_LAUNCH_TARGET"' in runbook
    assert '"$DESKTOP_DIST_DIR"' in runbook
    assert '"$DESKTOP_RELEASE_VERSION"' in runbook
    assert 'echo "== desktop packaging extract =="' in runbook


def test_runbook_downloads_smoke_stages_bootstrap_complete_preview_fixture() -> None:
    runbook = (REPO_ROOT / "scripts" / "runbook.sh").read_text(encoding="utf-8")

    assert 'mkdir -p "$DOWNLOADS_SMOKE_BUNDLE_DIR/files" "$DOWNLOADS_SMOKE_BUNDLE_DIR/startup-smoke" "$DOWNLOADS_SMOKE_DEPLOY_DIR"' in runbook
    assert 'startup_smoke_dir="$DOWNLOADS_SMOKE_BUNDLE_DIR/startup-smoke"' in runbook
    assert 'payload_path="$DOWNLOADS_SMOKE_BUNDLE_DIR/files/chummer-avalonia-win-x64-payload.zip"' in runbook
    assert 'BOOTSTRAP_METADATA_MARKER = b"\\nCHUMMER6_BOOTSTRAP_METADATA\\n"' in runbook
    assert '"contractName": "chummer6-ui.windows_bootstrap_payload"' in runbook
    assert '"artifactId": "avalonia-linux-x64-installer"' in runbook
    assert '"artifactId": "avalonia-win-x64-installer"' in runbook
    assert '"installerMode": "bootstrap"' in runbook
    assert '"payloadFileName": payload_file_name' in runbook
    assert '"payloadDownloadUrl": payload_download_url' in runbook
    assert '"payloadSha256": payload_sha256' in runbook
    assert '"payloadSizeBytes": payload_size' in runbook
    assert '"channel": "preview"' in runbook
    assert '"channelId": "preview"' in runbook
    assert 'archive.writestr("Chummer.Avalonia.exe", b"downloads smoke avalonia binary\\n")' in runbook
    assert 'archive.writestr("Samples/Legacy/Soma-Career.chum5", b"downloads smoke sample character\\n")' in runbook
    assert '"readyCheckpoint": "pre_ui_event_loop"' in runbook
    assert '"artifactRelativePath": f"files/{installer_name}"' in runbook
    assert '"artifactDigest": f"sha256:{artifact_sha256}"' in runbook
    assert '"hostClass": host_class' in runbook
    assert '"operatingSystem": operating_system' in runbook
    assert '"bootstrapPayloadAcquisitionMode": "download" if platform == "windows" else ""' in runbook
    assert '"bootstrapPayloadFileName": payload_file_name if platform == "windows" else ""' in runbook
    assert '"bootstrapPayloadSha256": payload_sha256 if platform == "windows" else ""' in runbook
    assert '"bootstrapPayloadSizeBytes": payload_size if platform == "windows" else 0' in runbook
    assert 'published_at = datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")' in runbook
    assert '"publishedAt": published_at' in runbook
    assert '"completedAtUtc": published_at' in runbook
    assert '(startup_smoke_dir / "windows-installer-progress-avalonia-win-x64.log").write_text(' in runbook
    assert '"Bootstrap temp root: C:\\\\Temp\\\\chummer-bootstrap"' in runbook
    assert 'f"Payload download target: C:\\\\Temp\\\\chummer-bootstrap\\\\{payload_file_name}"' in runbook
    assert '"Downloading application files - 100% at 12.3 MB/s"' in runbook
    assert '"Verifying payload size"' in runbook
    assert '"Verifying payload checksum"' in runbook
    assert '"Extracting application files"' in runbook
    assert '"Install complete"' in runbook
    assert 'RUNBOOK_MODE=downloads-sync \\' in runbook
    assert 'DOWNLOAD_BUNDLE_DIR="$DOWNLOADS_SMOKE_BUNDLE_DIR" \\' in runbook
    assert 'DOWNLOAD_DEPLOY_DIR="$DOWNLOADS_SMOKE_DEPLOY_DIR" \\' in runbook
    assert 'CHUMMER_ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH=1 \\' in runbook
    assert 'DOWNLOADS_SYNC_VERIFY_LINKS=1 \\' in runbook
    assert 'RUNBOOK_MODE=downloads-verify \\' in runbook
    assert 'DOWNLOADS_VERIFY_TARGET="$DOWNLOADS_SMOKE_DEPLOY_DIR/releases.json" \\' in runbook
    assert 'DOWNLOADS_VERIFY_LINKS=1 \\' in runbook
    assert 'echo "downloads-smoke sync_status=$sync_status verify_status=$verify_status"' in runbook


def test_public_stable_blocker_truth_guard_is_documented_for_self_hosted_release_ops() -> None:
    runbook = (REPO_ROOT / "docs" / "SELF_HOSTED_DOWNLOADS_RUNBOOK.md").read_text(encoding="utf-8")
    env_example = (REPO_ROOT / "docs" / "examples" / "self-hosted-downloads.env.example").read_text(encoding="utf-8")

    assert "RELEASE_BLOCKERS.generated.json" in runbook
    assert "CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS" in runbook
    assert "default max age is `86400` seconds" in runbook
    assert "adjusted blocker-truth window" in runbook
    assert "# CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS=86400" in env_example
    assert "Keep CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS at 86400" in env_example


def test_codex_wrappers_resolve_repo_root_from_script_location() -> None:
    for script_path in AI_CODEX_WRAPPERS:
        text = script_path.read_text(encoding="utf-8")

        assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"' in text
        assert 'source "$SCRIPT_DIR/_env.sh"' in text
        assert 'cd "$REPO_ROOT"' in text
        assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"' not in text
        assert 'REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"' not in text
        assert 'source "$REPO_ROOT/scripts/ai/_env.sh"' not in text
        assert 'cd "/docker/chummercomplete/chummer-presentation"' not in text


def test_day1_wrappers_resolve_repo_root_from_shared_env_contract() -> None:
    for script_path in AI_DAY1_WRAPPERS:
        text = script_path.read_text(encoding="utf-8")

        assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"' in text
        assert 'source "$SCRIPT_DIR/_env.sh"' in text
        assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"' not in text
        assert 'REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"' not in text
        assert 'repo_root="$(cd "$SCRIPT_DIR/../.." && pwd)"' not in text
        assert 'cd "/docker/chummercomplete/chummer-presentation"' not in text


def test_shared_env_utility_wrappers_use_script_dir_env_contract() -> None:
    for script_path in AI_SHARED_ENV_UTILITY_WRAPPERS:
        text = script_path.read_text(encoding="utf-8")

        assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"' in text
        assert 'source "$SCRIPT_DIR/_env.sh"' in text
        assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"' not in text
        assert 'source "$(dirname "$0")/_env.sh"' not in text
        assert 'REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"' not in text


def test_day1_setup_avoids_bash4_collectors_and_associative_arrays() -> None:
    text = AI_DAY1_SETUP.read_text(encoding="utf-8")

    assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"' in text
    assert 'source "$SCRIPT_DIR/_env.sh"' in text
    assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"' not in text
    assert 'repo_root="$REPO_ROOT"' in text
    assert 'cd "$repo_root"' in text
    assert 'repo_root="$(cd "$SCRIPT_DIR/../.." && pwd)"' not in text
    assert "collect_solution_projects()" in text
    assert "array_contains_exact()" in text
    assert "existing_projects=()" in text
    assert "while IFS= read -r existing_project; do" in text
    assert 'existing_projects+=("$existing_project")' in text
    assert '! array_contains_exact "$project" "${desired_projects[@]}"' in text
    assert '! array_contains_exact "$project" "${existing_projects[@]}"' in text
    assert "mapfile -t existing_projects" not in text
    assert "declare -A desired_lookup=()" not in text
    assert "declare -A existing_lookup=()" not in text


def test_latest_nightly_publish_preflights_windows_bootstrap_payload_metadata() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert_release_script_uses_alias_safe_repo_root(REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh")
    assert 'WORKSPACE_ROOT="$(cd "$REPO_ROOT_PHYSICAL/.." && pwd -P)"' in publisher
    assert 'WORKSPACE_ROOT="$(cd "$REPO_ROOT/.." && pwd)"' not in publisher
    assert "verify_latest_stage_windows_payload_gate()" in publisher
    assert "verify-windows-installer-payloads.py" in publisher
    assert "--require-embedded-bootstrap-metadata" in publisher
    assert "--require-manifest-row" in publisher
    assert "--allow-empty" in publisher
    assert "Nightly stage failed Windows installer payload preflight. Build a fresh stage before publishing." in publisher
    assert publisher.index('verify_latest_stage_windows_payload_gate "$latest_stage"') < publisher.index('echo "Publishing latest nightly stage: $latest_stage"')


def test_generate_releases_manifest_keeps_presentation_mirror_repo_local_by_default() -> None:
    manifest_script = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert 'PRESENTATION_MIRROR_ROOT="${PRESENTATION_MIRROR_ROOT:-$REPO_ROOT}"' in manifest_script
    assert 'PRESENTATION_MIRROR_ROOT="${PRESENTATION_MIRROR_ROOT:-/docker/chummercomplete/chummer-presentation}"' not in manifest_script
    assert 'repo_root_physical="$(cd "$REPO_ROOT" && pwd -P)"' in manifest_script
    assert 'mirror_root_physical="$(cd "$PRESENTATION_MIRROR_ROOT" && pwd -P)"' in manifest_script
    assert '[[ "$repo_root_physical" != "$mirror_root_physical" ]]' in manifest_script
    assert 'sync_presentation_downloads_mirror \\' in manifest_script
    assert '"$PRESENTATION_MIRROR_ROOT/Docker/Downloads/releases.json"' in manifest_script


def test_generate_releases_manifest_uses_repo_local_or_configured_ui_localization_gate_roots() -> None:
    manifest_script = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert 'resolve_ui_localization_release_gate_generator_root()' in manifest_script
    assert '"$REPO_ROOT"' in manifest_script
    assert '"$REPO_ROOT/../chummer6-ui"' in manifest_script
    assert '"$PRESENTATION_MIRROR_ROOT"' in manifest_script
    assert '"$PRESENTATION_MIRROR_ROOT/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"' in manifest_script
    assert '"/docker/chummercomplete/chummer-presentation/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"' not in manifest_script
    assert '"/docker/chummercomplete/chummer6-ui/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"' not in manifest_script
    assert '"/docker/chummercomplete/chummer-presentation"' not in manifest_script
    assert '"/docker/chummercomplete/chummer6-ui"' not in manifest_script


def test_generate_releases_manifest_only_cleans_tmp_gate_artifacts_outside_repo_root() -> None:
    manifest_script = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "path_is_tmp_outside_repo()" in manifest_script
    assert 'resolved_candidate="$(resolve_path_allow_missing "$candidate")"' in manifest_script
    assert 'resolved_repo_root="$(resolve_path_allow_missing "$REPO_ROOT")"' in manifest_script
    assert '[[ "$resolved_candidate" == /tmp/* && "$resolved_candidate" != "$resolved_repo_root" && "$resolved_candidate" != "$resolved_repo_root/"* ]]' in manifest_script
    assert 'if path_is_tmp_outside_repo "$RELEASE_PROOF_PATH"; then' in manifest_script
    assert 'if path_is_tmp_outside_repo "$UI_LOCALIZATION_RELEASE_GATE_PATH"; then' in manifest_script
    assert 'if [[ "$RELEASE_PROOF_PATH" == /tmp/* ]]; then' not in manifest_script
    assert 'if [[ "$UI_LOCALIZATION_RELEASE_GATE_PATH" == /tmp/* ]]; then' not in manifest_script


def test_latest_nightly_publish_ignores_incomplete_helper_stage_directories() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert "is_publishable_nightly_stage()" in publisher
    assert 'echo "Nightly staging root not found: $STAGING_ROOT"' in publisher
    assert 'verify_latest_stage_layout "$STAGING_ROOT"' in publisher
    assert 'if is_publishable_nightly_stage "$STAGING_ROOT"; then' in publisher
    assert 'latest_stage="$STAGING_ROOT"' in publisher
    assert '[[ -f "$stage_dir/RELEASE_CHANNEL.generated.json" ]] || return 1' in publisher
    assert '[[ -f "$stage_dir/releases.json" ]] || return 1' in publisher
    assert '[[ -d "$stage_dir/files" ]] || return 1' in publisher
    assert 'if ! is_publishable_nightly_stage "$candidate"; then' in publisher
    assert 'echo "No publishable nightly stage found under $STAGING_ROOT"' in publisher
    assert publisher.index('echo "Nightly staging root not found: $STAGING_ROOT" >&2') < publisher.index('latest_stage=""')
    assert publisher.index('if is_publishable_nightly_stage "$STAGING_ROOT"; then') < publisher.index('while IFS= read -r candidate; do')
    assert publisher.index('if ! is_publishable_nightly_stage "$candidate"; then') < publisher.index('latest_stage="$candidate"')


def test_latest_nightly_publish_rejects_nested_files_stage_layout_before_payload_preflight() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert "verify_latest_stage_layout()" in publisher
    assert 'echo "Nightly staging root points at files/ directory: $normalized_stage_dir"' in publisher
    assert 'local nested_files_dir="$files_dir/files"' in publisher
    assert 'echo "Nightly stage is malformed: found nested files directory under $nested_files_dir"' in publisher
    assert 'echo "Build the nightly stage root, not its files/ child, before publishing."' in publisher
    assert 'verify_latest_stage_layout "$latest_stage"' in publisher
    assert publisher.index('verify_latest_stage_layout "$latest_stage"') < publisher.index('verify_latest_stage_windows_payload_gate "$latest_stage"')


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
    assert "Windows visual proof next action:" in publisher
    assert "Nightly stage failed Windows desktop exit gate preflight. Use the Windows visual proof handoff above before publishing." in publisher
    assert "Nightly stage failed Windows installer startup smoke preflight. Build and smoke-test a fresh stage before publishing." in publisher
    assert publisher.index('verify_latest_stage_windows_payload_gate "$latest_stage"') < publisher.index('verify_latest_stage_windows_startup_smoke_gate "$latest_stage"')
    assert publisher.index('verify_latest_stage_windows_startup_smoke_gate "$latest_stage"') < publisher.index('verify_latest_stage_windows_exit_gate "$latest_stage"')
    assert publisher.index('verify_latest_stage_windows_exit_gate "$latest_stage"') < publisher.index('echo "Publishing latest nightly stage: $latest_stage"')
    assert 'row_platform_id = norm(row.get("platformId"))' in verifier
    assert 'normalized_arch = normalized_rid.rsplit("-", 1)[-1] if "-" in normalized_rid else normalized_rid' in verifier
    assert 'elif norm(row.get("arch")) != normalized_arch:' in verifier


def test_latest_nightly_publish_fail_closes_public_edge_redeploy_until_hub_postdeploy_receipts_are_enrolled() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert 'PUBLIC_EDGE_VERIFY_BASE_URL="${CHUMMER_PUBLIC_EDGE_VERIFY_BASE_URL:-http://127.0.0.1:${CHUMMER_PUBLIC_EDGE_PORT:-8091}}"' in publisher
    assert 'PUBLIC_EDGE_VERIFY_HOST="${CHUMMER_PUBLIC_EDGE_VERIFY_HOST:-chummer.run}"' in publisher
    assert 'PUBLIC_EDGE_VERIFY_PROTO="${CHUMMER_PUBLIC_EDGE_VERIFY_PROTO:-https}"' in publisher
    assert "validate_absolute_http_url()" in publisher
    assert "validate_http_host_header()" in publisher
    assert "validate_forwarded_proto()" in publisher
    assert 'validate_absolute_http_url "$PUBLIC_EDGE_VERIFY_BASE_URL" "CHUMMER_PUBLIC_EDGE_VERIFY_BASE_URL"' in publisher
    assert 'validate_http_host_header "$PUBLIC_EDGE_VERIFY_HOST" "CHUMMER_PUBLIC_EDGE_VERIFY_HOST"' in publisher
    assert 'validate_forwarded_proto "$PUBLIC_EDGE_VERIFY_PROTO" "CHUMMER_PUBLIC_EDGE_VERIFY_PROTO"' in publisher
    assert "verify_public_edge_open_public_install_routes()" in publisher
    assert 'for key in ("downloads", "artifacts"):' in publisher
    assert 'install_access_class == "open_public"' in publisher
    assert 'expected_location = f"/downloads/get/{artifact_id}"' in publisher
    assert 'redirected back to login instead of direct public download' in publisher
    assert 'Published downloads shelf failed open-public installer route verification.' in publisher
    assert 'verify_public_edge_open_public_install_routes \\' not in publisher
    assert 'docker compose -f docker-compose.public-edge.yml up -d' not in publisher
    assert 'Windows-only nightly publication cannot redeploy the public edge until an authoritative Hub postdeploy receipt schema is enrolled.' in publisher
    assert 'Set CHUMMER_REDEPLOY_PUBLIC_EDGE_AFTER_NIGHTLY_PUBLISH=false for local shelf activation only.' in publisher
    assert 'Activated the Windows-only nightly on the local downloads shelf; external Hub convergence remains unverified.' in publisher
    assert publisher.index('if to_bool "$REDEPLOY_PUBLIC_EDGE"; then') < publisher.index('echo "Publishing latest nightly stage: $latest_stage"')
    assert publisher.index('validate_absolute_http_url "$PUBLIC_EDGE_VERIFY_BASE_URL" "CHUMMER_PUBLIC_EDGE_VERIFY_BASE_URL"') < publisher.index('bash "$SCRIPT_DIR/publish-download-bundle.sh" "$latest_stage/publication" "$DEPLOY_DIR"')


def test_latest_nightly_publish_remains_preview_handoff_lane() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert 'PUBLIC_RELEASE_CHANNEL="${CHUMMER_PUBLIC_DEFAULT_RELEASE_CHANNEL:-preview}"' in publisher
    assert 'ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH="${CHUMMER_ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH:-0}"' in publisher
    assert "Nightly publisher is the preview handoff lane. Refusing stable/public_stable publication from this script." in publisher
    assert "is_publishable_nightly_stage()" in publisher
    assert 'if ! is_publishable_nightly_stage "$candidate"; then' in publisher
    assert "No publishable nightly stage found under $STAGING_ROOT" in publisher


def test_latest_nightly_publish_prevalidates_public_edge_postdeploy_probe_config() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-latest-nightly-to-downloads.sh").read_text(encoding="utf-8")

    assert 'if to_bool "$REDEPLOY_PUBLIC_EDGE" && [[ "$DEPLOY_DIR" == "$WORKSPACE_ROOT/chummer.run-services/Chummer.Portal/downloads" ]]; then' in publisher
    assert "expected bare host header value" in publisher
    assert "expected 'http' or 'https'" in publisher
    assert publisher.index('validate_forwarded_proto "$PUBLIC_EDGE_VERIFY_PROTO" "CHUMMER_PUBLIC_EDGE_VERIFY_PROTO"') < publisher.index('refresh_release_build_handoff()')


def test_release_support_shell_scripts_use_alias_safe_repo_root() -> None:
    for script_path in RELEASE_SUPPORT_ALIAS_SAFE_SCRIPTS:
        assert_release_script_uses_alias_safe_repo_root(script_path)


def test_resolve_hub_registry_root_prefers_physical_workspace_sibling_candidates() -> None:
    resolver = (REPO_ROOT / "scripts" / "resolve-hub-registry-root.sh").read_text(encoding="utf-8")

    assert 'WORKSPACE_ROOT="$(cd "$REPO_ROOT_PHYSICAL/.." && pwd -P)"' in resolver
    assert 'echo "Configured CHUMMER_HUB_REGISTRY_ROOT does not exist: $explicit_registry_root"' in resolver
    assert 'echo "Configured CHUMMER_HUB_REGISTRY_ROOT is not a hub registry repo root: $explicit_registry_root"' in resolver
    assert 'echo "Expected scripts/materialize_public_release_channel.py or scripts/verify_public_release_channel.py under that directory."' in resolver
    assert '"${WORKSPACE_ROOT}/chummer6-hub-registry"' in resolver
    assert '"${WORKSPACE_ROOT}/chummer-hub-registry"' in resolver
    assert '"${REPO_ROOT}/../chummer6-hub-registry"' not in resolver
    assert '"${REPO_ROOT}/../chummer-hub-registry"' not in resolver
    assert '"$(cd "${REPO_ROOT}/.." && pwd)/chummer6-hub-registry"' not in resolver
    assert '"$(cd "${REPO_ROOT}/.." && pwd)/chummer-hub-registry"' not in resolver
    assert resolver.index('if [[ -n "${CHUMMER_HUB_REGISTRY_ROOT:-}" ]]; then') < resolver.index('declare -a candidates=()')


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


def test_s3_publish_windows_payload_gate_allows_empty_only_before_installers_are_added() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-download-bundle-s3.sh").read_text(encoding="utf-8")

    assert "windows_payload_gate_args=(" in publisher
    assert "array_count()" in publisher
    assert "--files-dir \"$FILES_SOURCE\"" in publisher
    assert "--manifest \"$MANIFEST_SOURCE\"" in publisher
    assert "--require-embedded-bootstrap-metadata" in publisher
    assert "--require-manifest-row" in publisher
    assert 'windows_payload_gate_args_count="$(array_count windows_payload_gate_args)"' in publisher
    assert 'if (( windows_payload_gate_args_count == 6 )); then' in publisher
    assert "--allow-empty" in publisher


def test_s3_publish_rejects_nested_files_layout_before_payload_preflight() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-download-bundle-s3.sh").read_text(encoding="utf-8")

    assert "verify_bundle_layout()" in publisher
    assert 'echo "Bundle root points at files/ directory: $normalized_bundle_dir"' in publisher
    assert 'local nested_files_dir="$files_dir/files"' in publisher
    assert 'echo "Bundle is malformed: found nested files directory under $nested_files_dir"' in publisher
    assert 'echo "Publish from the stage or bundle root, not its files/ child."' in publisher
    assert 'verify_bundle_layout "$BUNDLE_DIR" "$FILES_SOURCE"' in publisher
    assert publisher.index('verify_bundle_layout "$BUNDLE_DIR" "$FILES_SOURCE"') < publisher.index('python3 "$SCRIPT_DIR/verify-windows-installer-payloads.py"')


def test_s3_publish_validates_object_storage_and_verify_config_before_manifest_regeneration() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-download-bundle-s3.sh").read_text(encoding="utf-8")

    assert "validate_s3_uri()" in publisher
    assert "validate_absolute_http_url()" in publisher
    assert "expected s3://bucket/path URI" in publisher
    assert "expected absolute http:// or https:// URL" in publisher
    assert 'validate_s3_uri "$S3_TARGET_URI" "CHUMMER_PORTAL_DOWNLOADS_S3_URI"' in publisher
    assert 'validate_s3_uri "$S3_LATEST_URI" "CHUMMER_PORTAL_DOWNLOADS_S3_LATEST_URI"' in publisher
    assert 'validate_absolute_http_url "$VERIFY_URL" "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"' in publisher
    assert 'validate_absolute_http_url "$S3_ENDPOINT_URL" "CHUMMER_PORTAL_DOWNLOADS_S3_ENDPOINT_URL"' in publisher
    assert publisher.index('validate_s3_uri "$S3_TARGET_URI" "CHUMMER_PORTAL_DOWNLOADS_S3_URI"') < publisher.index('bash "$SCRIPT_DIR/generate-releases-manifest.sh"')
    assert publisher.index('validate_absolute_http_url "$VERIFY_URL" "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"') < publisher.index('bash "$SCRIPT_DIR/generate-releases-manifest.sh"')


def test_s3_publish_reports_missing_bundle_root_before_layout_checks() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-download-bundle-s3.sh").read_text(encoding="utf-8")

    assert 'echo "Bundle directory not found: $BUNDLE_DIR"' in publisher
    assert publisher.index('echo "Bundle directory not found: $BUNDLE_DIR" >&2') < publisher.index('echo "Expected desktop-download-bundle layout: releases.json + files/chummer-*" >&2')


def test_http_publish_rejects_nested_files_layout_before_payload_preflight() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-download-bundle-http.sh").read_text(encoding="utf-8")

    assert "verify_bundle_layout()" in publisher
    assert 'echo "Bundle root points at files/ directory: $normalized_bundle_dir"' in publisher
    assert 'local nested_files_dir="$files_dir/files"' in publisher
    assert 'echo "Bundle is malformed: found nested files directory under $nested_files_dir"' in publisher
    assert 'echo "Publish from the stage or bundle root, not its files/ child."' in publisher
    assert 'verify_bundle_layout "$BUNDLE_DIR" "$BUNDLE_DIR/files"' in publisher
    assert publisher.index('verify_bundle_layout "$BUNDLE_DIR" "$BUNDLE_DIR/files"') < publisher.index('python3 "$SCRIPT_DIR/verify-windows-installer-payloads.py"')


def test_http_publish_validates_upload_and_verify_urls_before_dry_run_or_network() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-download-bundle-http.sh").read_text(encoding="utf-8")

    assert "validate_absolute_http_url()" in publisher
    assert "expected absolute http:// or https:// URL" in publisher
    assert 'validate_absolute_http_url "$UPLOAD_URL" "CHUMMER_RELEASE_UPLOAD_URL"' in publisher
    assert 'validate_absolute_http_url "$SESSIONS_URL" "CHUMMER_RELEASE_UPLOAD_SESSIONS_URL"' in publisher
    assert 'validate_absolute_http_url "$VERIFY_URL" "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"' in publisher
    assert publisher.index('validate_absolute_http_url "$UPLOAD_URL" "CHUMMER_RELEASE_UPLOAD_URL"') < publisher.index('if to_bool "$DRY_RUN"; then')
    assert publisher.index('validate_absolute_http_url "$VERIFY_URL" "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"') < publisher.index('if to_bool "$DRY_RUN"; then')
    assert publisher.index('validate_absolute_http_url "$SESSIONS_URL" "CHUMMER_RELEASE_UPLOAD_SESSIONS_URL"') < publisher.index('if ! resolve_upload_token; then')


def test_windows_bootstrap_build_is_measured_by_the_real_payload_gate() -> None:
    builder = (REPO_ROOT / "scripts" / "build-desktop-installer.sh").read_text(encoding="utf-8")
    native_builder = (REPO_ROOT / "scripts" / "build-native-windows-bootstrap-installer.sh").read_text(encoding="utf-8")
    bootstrap_template = (REPO_ROOT / "scripts" / "windows-bootstrap" / "installer.nsi").read_text(encoding="utf-8")

    assert 'local installer_mode="${CHUMMER_WINDOWS_INSTALLER_MODE:-bootstrap}"' in builder
    assert 'if [[ "$(basename "$DIST_DIR")" == "files" ]]; then' in builder
    assert "Refusing to use a downloads files/ directory as the desktop installer dist root" in builder
    assert "Pass the release stage root" in builder
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

    assert 'SCRIPT_DIR_PHYSICAL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"' in smoke
    assert 'REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR_PHYSICAL/.." && pwd -P)"' in smoke
    assert 'REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"' in smoke
    assert 'SCRIPT_DIR="$REPO_ROOT/scripts"' in smoke
    assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"' not in smoke
    assert 'chummerwinsmokeXXXXXX' in smoke
    assert 'local payload_name="${artifact_name%-installer.exe}-payload.zip"' in smoke
    assert 'local_payload_path="$artifact_dir/files/$payload_name"' in smoke
    assert "WINDOWS_LOCAL_PAYLOAD_COPY" in smoke
    assert "winepath -u 'C:\\\\windows\\\\temp'" in smoke
    assert 'cp "$local_payload_path" "$WINDOWS_LOCAL_PAYLOAD_COPY"' in smoke
    assert 'configured_payload_mode="$(lower_ascii "${WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE:-}")"' in smoke
    assert 'if [[ -n "$local_payload_path" ]]; then' in smoke
    assert 'configured_payload_mode="download"' in smoke
    assert 'CHUMMER_INSTALLER_PAYLOAD_PATH="$(to_native_path "$local_payload_path")"' in smoke
    assert 'CHUMMER_INSTALLER_PAYLOAD_SHA256="$local_payload_sha256"' in smoke
    assert 'CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES="$local_payload_size_bytes"' in smoke


def test_release_manifest_generation_prunes_install_proof_routes_to_published_artifacts() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "prune_release_proof_routes_to_manifest_artifacts" in generator
    assert 'route.startswith("/downloads/install/")' in generator
    assert 'artifact_id in artifact_ids' in generator
    assert 'release_proof["proofRoutes"] = prune_routes' in generator


def test_release_manifest_generation_can_skip_external_host_proof_blockers_for_artifact_only_publish_paths() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert 'GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS="${CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS:-1}"' in generator
    assert 'if to_bool "$GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS"; then' in generator
    assert 'materialize-external-host-proof-blockers.py' in generator
    assert 'echo "skipped external host proof blocker materialization"' in generator


def test_release_manifest_generation_uses_portable_release_channel_normalization() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "lower_ascii()" in generator
    assert 'if [[ "$(lower_ascii "$RELEASE_CHANNEL")" == "preview" ]]; then' in generator
    assert "${RELEASE_CHANNEL,,}" not in generator


def test_release_manifest_generation_uses_registry_review_required_supportability_language() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "Treat this shelf as review-required until stale or incomplete proof receipts are refreshed." in generator
    assert "The preview shelf remains visible, but stale or incomplete proof receipts mean it is not yet gold-ready." in generator


def test_ui_release_shell_scripts_use_nounset_safe_array_count() -> None:
    for script_path, expectations in RELEASE_ARRAY_PORTABILITY_EXPECTATIONS.items():
        text = script_path.read_text(encoding="utf-8")

        assert_release_script_uses_alias_safe_repo_root(script_path)

        for snippet in ARRAY_COUNT_HELPER_SNIPPETS:
            assert snippet in text, f"missing nounset-safe array_count helper snippet in {script_path}: {snippet}"
        assert 'eval "set -- \\${${array_name}[@]+\\"\\${${array_name}[@]}\\"}"' not in text

        for snippet in expectations["required"]:
            assert snippet in text, f"missing expected portability usage in {script_path}: {snippet}"

        for snippet in expectations["forbidden"]:
            assert snippet not in text, f"found bash3-unsafe raw array length expansion in {script_path}: {snippet}"


def test_verify_releases_manifest_rejects_downloads_files_child_and_missing_local_root_manifest() -> None:
    verifier = (REPO_ROOT / "scripts" / "verify-releases-manifest.sh").read_text(encoding="utf-8")

    assert 'if [[ -d "$TARGET" ]]; then' in verifier
    assert 'echo "Verification target points at downloads files/ directory: $normalized_target"' in verifier
    assert 'echo "Verify the downloads shelf root or its releases.json manifest, not its files/ child."' in verifier
    assert 'target_manifest_path="$normalized_target/releases.json"' in verifier
    assert 'echo "Local downloads shelf directory is missing releases.json: $target_manifest_path"' in verifier
    assert 'TARGET="$target_manifest_path"' in verifier
    assert verifier.index('if [[ -d "$TARGET" ]]; then') < verifier.index('python3 "$REGISTRY_ROOT/scripts/verify_public_release_channel.py"')


def test_runbook_release_shell_scripts_use_alias_safe_repo_root() -> None:
    for script_path in RUNBOOK_ALIAS_SAFE_SCRIPTS:
        assert_release_script_uses_alias_safe_repo_root(script_path)


def test_runbook_focused_presentation_mode_uses_nounset_safe_array_count() -> None:
    runbook = (REPO_ROOT / "scripts" / "runbook.sh").read_text(encoding="utf-8")

    for snippet in ARRAY_COUNT_HELPER_SNIPPETS:
        assert snippet in runbook, f"missing nounset-safe array_count helper snippet in runbook.sh: {snippet}"

    assert 'focused_test_support_arg_count="$(array_count focused_test_support_args)"' in runbook
    assert 'if (( focused_test_support_arg_count > 0 )); then' in runbook
    assert 'focused_test_prerequisite_project_count="$(array_count focused_test_prerequisite_projects)"' in runbook
    assert 'if (( focused_test_prerequisite_project_count > 0 )); then' in runbook
    assert '${#focused_test_support_args[@]}' not in runbook
    assert '${#focused_test_prerequisite_projects[@]}' not in runbook


def test_runbook_and_host_prereq_scripts_validate_nuget_endpoints_before_python_probes() -> None:
    prereqs = (REPO_ROOT / "scripts" / "check-host-gate-prereqs.sh").read_text(encoding="utf-8")
    runbook = (REPO_ROOT / "scripts" / "runbook.sh").read_text(encoding="utf-8")

    assert "validate_host_port_endpoint()" in prereqs
    assert "validate_host_port_endpoint()" in runbook
    assert "expected host:port with numeric port 1-65535" in prereqs
    assert "expected host:port with numeric port 1-65535" in runbook
    assert 'if ! endpoint_validation_error="$(validate_host_port_endpoint "$NUGET_ENDPOINT" "NUGET_ENDPOINT")"; then' in prereqs
    assert 'if ! validate_host_port_endpoint "$TEST_NUGET_ENDPOINT" "TEST_NUGET_ENDPOINT"; then' in runbook
    assert prereqs.index('if ! endpoint_validation_error="$(validate_host_port_endpoint "$NUGET_ENDPOINT" "NUGET_ENDPOINT")"; then') < prereqs.index('python3 - "$host" "$port" <<\'PY\' >"$NUGET_LOG_FILE" 2>&1')
    assert runbook.index('if ! validate_host_port_endpoint "$TEST_NUGET_ENDPOINT" "TEST_NUGET_ENDPOINT"; then') < runbook.index('python3 - "$host" "$port" <<\'PY\' >/dev/null 2>&1')


def test_focused_presentation_runbook_rebuilds_presentation_before_focused_test_host() -> None:
    runbook = (REPO_ROOT / "scripts" / "runbook.sh").read_text(encoding="utf-8")

    assert 'if [[ "$RUNBOOK_MODE" == "focused-presentation-tests" ]]; then' in runbook
    assert 'FOCUSED_TEST_FRAMEWORK="${FOCUSED_TEST_FRAMEWORK:-net10.0}"' in runbook
    assert 'FOCUSED_TEST_PREREQUISITE_PROJECTS="${FOCUSED_TEST_PREREQUISITE_PROJECTS:-${FOCUSED_TEST_PREREQUISITE_PROJECT:-Chummer.Presentation/Chummer.Presentation.csproj}}"' in runbook
    assert 'IFS=\'|\' read -r -a focused_test_prerequisite_projects <<< "$FOCUSED_TEST_PREREQUISITE_PROJECTS"' in runbook
    assert 'dotnet build "$prerequisite_project"' in runbook
    assert '-f "$FOCUSED_TEST_FRAMEWORK"' in runbook
    assert '== focused presentation prerequisite build failure extract ==' in runbook
    assert 'dotnet build "$FOCUSED_TEST_PROJECT"' in runbook
    assert 'TEST_RUNNER_PATH="$(resolve_mtp_test_runner "$FOCUSED_TEST_PROJECT" "$FOCUSED_TEST_CONFIGURATION" "$FOCUSED_TEST_FRAMEWORK")"' in runbook
    assert runbook.index('dotnet build "$prerequisite_project"') < runbook.index('dotnet build "$FOCUSED_TEST_PROJECT"')


def test_focused_presentation_test_project_supports_pipe_delimited_helper_files() -> None:
    project = (REPO_ROOT / "Chummer.Tests" / "Chummer.Tests.csproj").read_text(encoding="utf-8")

    assert "<FocusedTestSupportFiles Condition=\"'$(FocusedTestSupportFiles)' == ''\"></FocusedTestSupportFiles>" in project
    assert "<ResolvedFocusedTestSupportFiles Condition=\"'$(FocusedTestSupportFiles)' != ''\">$([System.String]::Copy('$(FocusedTestSupportFiles)').Replace('|', ';'))</ResolvedFocusedTestSupportFiles>" in project
    assert "<_FocusedTestSupportFile Include=\"$([MSBuild]::Unescape('$(ResolvedFocusedTestSupportFiles)'))\" Condition=\"'$(ResolvedFocusedTestSupportFiles)' != ''\" />" in project
    assert "<Compile Include=\"@(_FocusedTestSupportFile)\" />" in project


def test_runbook_local_tests_supports_microsoft_testing_platform_direct_runner() -> None:
    runbook = (REPO_ROOT / "scripts" / "runbook.sh").read_text(encoding="utf-8")

    assert 'TEST_MTP_DIRECT_RUNNER="${TEST_MTP_DIRECT_RUNNER:-auto}"' in runbook
    assert "is_microsoft_testing_platform_runner()" in runbook
    assert 'runner = payload.get("test", {}).get("runner")' in runbook
    assert 'raise SystemExit(0 if runner == "Microsoft.Testing.Platform" else 1)' in runbook
    assert '&& is_microsoft_testing_platform_runner "$REPO_ROOT/global.json"; then' in runbook
    assert 'echo "local-tests using Microsoft.Testing.Platform direct runner"' in runbook
    assert 'dotnet build "$TEST_PROJECT" -c "$TEST_CONFIGURATION"' in runbook
    assert 'TEST_RUNNER_PATH="$(resolve_mtp_test_runner "$TEST_PROJECT" "$TEST_CONFIGURATION" "$TEST_FRAMEWORK")"' in runbook
    assert 'if [[ -z "$TEST_RUNNER_PATH" || ! -f "$TEST_RUNNER_PATH" ]]; then' in runbook
    assert 'echo "Unable to resolve Microsoft.Testing.Platform runner for $TEST_PROJECT." >&2' in runbook
    assert '"$TEST_RUNNER_PATH" "${runner_args[@]}" 2>&1 | tee -a "$TEST_LOG_FILE"' in runbook
    assert runbook.index('echo "local-tests using Microsoft.Testing.Platform direct runner"') < runbook.index('TEST_RUNNER_PATH="$(resolve_mtp_test_runner "$TEST_PROJECT" "$TEST_CONFIGURATION" "$TEST_FRAMEWORK")"')


def test_runbook_resolve_mtp_test_runner_checks_framework_then_bin_scan() -> None:
    runbook = (REPO_ROOT / "scripts" / "runbook.sh").read_text(encoding="utf-8")

    assert "resolve_mtp_test_runner()" in runbook
    assert 'candidate="$project_dir/bin/$configuration/$framework/$project_name"' in runbook
    assert 'candidate="$project_dir/bin/$configuration/$framework/$project_name.exe"' in runbook
    assert 'done < <(find "$project_dir/bin/$configuration" -mindepth 2 -maxdepth 2 -type f \\' in runbook
    assert '\\( -name "$project_name" -o -name "$project_name.exe" \\) | sort)' in runbook
    assert runbook.index('candidate="$project_dir/bin/$configuration/$framework/$project_name"') < runbook.index('done < <(find "$project_dir/bin/$configuration" -mindepth 2 -maxdepth 2 -type f \\')


def test_publish_download_bundle_defaults_external_host_proof_blockers_off_during_shelf_sync() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert 'CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS="${CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS:-0}" \\' in publish_script


def test_registry_manifest_fallback_is_permanently_disabled_without_sealed_authority() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "CHUMMER_ALLOW_AUTHORITY_BOUND_REGISTRY_FALLBACK" not in generator
    assert "CHUMMER_CANONICAL_RELEASE_TRUTH_PATH" not in generator
    assert "CHUMMER_CANONICAL_RELEASE_TRUTH_COMPATIBILITY_PATH" not in generator
    assert 'elif to_bool "$ALLOW_AUTHORITY_BOUND_REGISTRY_FALLBACK"; then' not in generator
    assert generator.count("restore_local_manifests_from_registry_if_needed") == 1
    function_start = generator.index("restore_local_manifests_from_registry_if_needed()")
    function_prefix = generator[function_start : function_start + 300]
    assert "registry manifest fallback is permanently disabled" in function_prefix
    assert "return 1" in function_prefix
    assert 'python3 "$REGISTRY_ROOT/scripts/verify_release_truth_mirror.py"' not in generator
    assert "registry manifest fallback permanently disabled; only this build's staged artifacts may define release truth" in generator


def test_startup_smoke_publication_sanitizes_runtime_and_artifact_host_paths(tmp_path: Path) -> None:
    smoke = (REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh").read_text(encoding="utf-8")

    assert 'payload["processPath"] = process_file_name or "<redacted:process-path>"' in smoke
    assert 'payload["processPathDisclosure"] = "file_name_only"' in smoke
    assert 'payload["artifactPath"] = artifact_relative_path' in smoke
    assert 'payload["artifactPathDisclosure"] = artifact_path_disclosure' in smoke
    assert '"startupReceiptPath": startup_receipt_name,' in smoke
    assert '"startupReceiptPathDisclosure": "file_name_only",' in smoke
    assert 'tail_lines = [redact_user_profile_paths(line) for line in raw_tail_lines]' in smoke

    fixture_launch = tmp_path / "fixture" / "Chummer.Avalonia"
    fixture_launch.parent.mkdir(parents=True)
    fixture_launch.write_text(
        """#!/usr/bin/python3
import json
import os
from pathlib import Path

receipt = Path(os.environ["CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT"])
receipt.write_text(json.dumps({
    "status": "passed",
    "headId": "avalonia",
    "version": "run-portable",
    "releaseVersion": "run-portable",
    "channelId": "docker",
    "platform": "linux",
    "arch": "x64",
    "rid": "linux-x64",
    "readyCheckpoint": "pre_ui_event_loop",
    "processPath": "/Users/José Runner/work/bin/Chummer.Avalonia",
    "artifactPath": "/private/var/folders/build/files/stale.tar.gz",
    "logPath": "/tmp/private/session.log",
    "note": "loaded from /home/Build User/work/state.json and /var/tmp/private/cache.bin",
}), encoding="utf-8")
""",
        encoding="utf-8",
    )
    fixture_launch.chmod(0o755)
    artifact = tmp_path / "private-host-build" / "files" / "chummer-test.tar.gz"
    artifact.parent.mkdir(parents=True)
    with tarfile.open(artifact, "w:gz") as archive:
        archive.add(fixture_launch, arcname="Chummer.Avalonia")
    output = tmp_path / "receipts"
    result = subprocess.run(
        [
            "bash",
            str(REPO_ROOT / "scripts" / "run-desktop-startup-smoke.sh"),
            str(artifact),
            "avalonia",
            "linux-x64",
            "Chummer.Avalonia",
            str(output),
            "run-portable",
        ],
        cwd=REPO_ROOT,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=os.environ,
        check=False,
    )
    assert result.returncode == 0, result.stdout + result.stderr
    receipt = json.loads(
        (output / "startup-smoke-avalonia-linux-x64.receipt.json").read_text(encoding="utf-8")
    )
    assert receipt["processPath"] == "Chummer.Avalonia"
    assert receipt["processPathDisclosure"] == "file_name_only"
    assert receipt["artifactPath"] == "files/chummer-test.tar.gz"
    assert receipt["artifactPathDisclosure"] == "artifact_shelf_relative_path"
    assert receipt["logPath"] == "session.log"
    serialized = json.dumps(receipt, ensure_ascii=False)
    for forbidden in (str(tmp_path), "José Runner", "Build User", "/tmp/", "/private/var/", "/var/tmp/"):
        assert forbidden not in serialized


def test_desktop_exit_gate_generators_project_embedded_receipts_portably(tmp_path: Path) -> None:
    linux_path = REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh"
    macos_path = REPO_ROOT / "scripts" / "materialize-macos-desktop-exit-gate.sh"
    linux_gate = linux_path.read_text(encoding="utf-8")
    macos_gate = macos_path.read_text(encoding="utf-8")

    for gate in (linux_gate, macos_gate):
        assert "def portable_receipt_projection(" in gate
        assert 'r"/Users/[^/\\r\\n]+/"' in gate
        assert 'r"(?i)[A-Z]:[\\\\/](?:Users|Documents and Settings)' in gate
        assert "private/var" in gate
        assert "var/tmp" in gate
        assert "run/user" in gate
    assert "installer_receipt = portable_receipt_projection(load_json(installer_receipt_path))" in linux_gate
    assert "archive_receipt = portable_receipt_projection(load_json(archive_receipt_path))" in linux_gate
    assert "startup_smoke_payload = portable_receipt_projection(load_json(startup_smoke_path))" in macos_gate
    assert "payload = portable_receipt_projection(payload)" in macos_gate

    fixture = {
        "status": "passed",
        "processPath": "/Users/José Runner/work/bin/Chummer.Avalonia",
        "artifactPath": "/private/var/folders/build/files/chummer-test.dmg",
        "log": "from /home/Build User/work and /tmp/secret/output.log",
        "nested": {
            "receipt_path": "/docker/chummer/run/startup-smoke/startup.receipt.json",
            "reason": "copied from C:\\Users\\Test User\\work\\result.json",
        },
        "startup_smoke": {
            "candidate_paths": [
                "/docker/chummer/startup-smoke/first.receipt.json",
                "/opt/chummer/proofs/second.receipt.json",
                "/srv/chummer/proofs/third.receipt.json",
            ],
            "artifact_path_candidates": [
                "/private/var/folders/build/files/chummer-test.dmg",
            ],
            "installer_primary_shelf_root": "/opt/chummer/private/release-shelf",
        },
    }
    fixture_path = tmp_path / "startup.receipt.json"
    fixture_path.write_text(json.dumps(fixture), encoding="utf-8")

    for gate_path, start_marker, end_marker, argument in (
        (linux_path, "def load_json(path_text: str):", "\ndef load_failure_reasons", str(fixture_path)),
        (macos_path, "def load_json(path: Path)", "\ndef write_json_atomic", fixture_path),
    ):
        source = gate_path.read_text(encoding="utf-8")
        start = source.index(start_marker)
        end = source.index(end_marker, start)
        namespace = {"json": json, "pathlib": __import__("pathlib"), "re": __import__("re"), "Path": Path, "Any": object, "Dict": dict}
        exec(source[start:end], namespace)
        loaded = namespace["load_json"](argument)
        projected = namespace["portable_receipt_projection"](loaded)
        serialized = json.dumps(projected, ensure_ascii=False)
        assert projected["status"] == "passed"
        assert projected["processPath"] == "Chummer.Avalonia"
        assert projected["processPathDisclosure"] == "file_name_only"
        assert projected["artifactPath"] == "files/chummer-test.dmg"
        assert projected["nested"]["receipt_path"] == "startup-smoke/startup.receipt.json"
        assert projected["nested"]["receipt_path_disclosure"] == "release_shelf_relative_path"
        assert projected["startup_smoke"]["candidate_paths"] == [
            "startup-smoke/first.receipt.json",
            "second.receipt.json",
            "third.receipt.json",
        ]
        artifact_candidates = projected["startup_smoke"]["artifact_path_candidates"]
        assert len(artifact_candidates) == 1
        assert not artifact_candidates[0].startswith(("/", "\\"))
        if gate_path == macos_path:
            assert artifact_candidates == ["files/chummer-test.dmg"]
        assert projected["startup_smoke"]["installer_primary_shelf_root"] == "release-shelf"
        for forbidden in (
            "José Runner",
            "Build User",
            "Test User",
            "/tmp/",
            "/private/var/",
            "/docker/",
            "/opt/",
            "/srv/",
        ):
            assert forbidden not in serialized


def test_publish_download_bundle_requires_explicit_opt_in_before_falling_back_to_unrelated_files_roots() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert 'ALLOW_BUNDLE_FILES_SOURCE_FALLBACK="${CHUMMER_ALLOW_BUNDLE_FILES_SOURCE_FALLBACK:-0}"' in publish_script
    assert 'if to_bool "$ALLOW_BUNDLE_FILES_SOURCE_FALLBACK"; then' in publish_script
    assert 'echo "Refusing to fall back to unrelated downloads/files roots unless CHUMMER_ALLOW_BUNDLE_FILES_SOURCE_FALLBACK=true is set explicitly."' in publish_script
    assert publish_script.index('if to_bool "$ALLOW_BUNDLE_FILES_SOURCE_FALLBACK"; then') < publish_script.index('echo "Bundle is missing files directory: $FILES_SOURCE" >&2')


def test_publish_download_bundle_defaults_cross_checkout_mirror_sync_to_repo_owned_live_roots() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert 'SYNC_LIVE_DOWNLOADS_MIRRORS="${CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS:-auto}"' in publish_script
    assert "normalize_mirror_sync_mode()" in publish_script
    assert 'printf \'%s\\n\' "auto"' in publish_script
    assert "deploy_dir_is_repo_owned_live_downloads_root()" in publish_script
    assert "deploy_dir_is_live_downloads_root()" in publish_script
    assert 'if [[ "$mode" == "auto" ]] && ! deploy_dir_is_repo_owned_live_downloads_root "$deploy_dir_physical"; then' in publish_script
    assert 'if [[ "$mode" != "auto" ]] && ! deploy_dir_is_live_downloads_root "$deploy_dir_physical"; then' in publish_script
    assert 'return 0' in publish_script
    assert 'configured="${CHUMMER_PUBLIC_EDGE_DOWNLOADS_MIRROR_DIRS:-}"' in publish_script
    assert 'sync_live_downloads_mirrors_mode="$(normalize_mirror_sync_mode "$SYNC_LIVE_DOWNLOADS_MIRRORS")"' in publish_script
    assert 'if [[ "$sync_live_downloads_mirrors_mode" != "false" ]]; then' in publish_script
    assert 'discover_live_downloads_mirror_dirs "$sync_live_downloads_mirrors_mode"' in publish_script
    assert '"$REPO_ROOT/Chummer.Portal/downloads" \\' in publish_script
    assert '"$REPO_ROOT/.codex-studio/published/portal" \\' in publish_script
    assert '"$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads" \\' in publish_script
    assert publish_script.index('if [[ "$mode" == "auto" ]] && ! deploy_dir_is_repo_owned_live_downloads_root "$deploy_dir_physical"; then') < publish_script.index('if [[ "$deploy_dir_physical" != "$canonical_downloads_physical" ]]; then')


def test_nightly_publish_path_uses_portable_missing_path_resolution() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")
    manifest_script = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "resolve_path_allow_missing()" in publish_script
    assert 'print(pathlib.Path(sys.argv[1]).resolve(strict=False))' in publish_script
    assert 'resolved_candidate="$(resolve_path_allow_missing "$candidate")"' in publish_script
    assert 'deploy_dir_physical="$(resolve_path_allow_missing "$DEPLOY_DIR")"' in publish_script
    assert 'resolved_target_dir="$(resolve_path_allow_missing "$target_dir")"' in publish_script
    assert "realpath -m" not in publish_script

    assert "resolve_path_allow_missing()" in manifest_script
    assert 'resolved_startup_smoke_dir="$(resolve_path_allow_missing "$STARTUP_SMOKE_DIR")"' in manifest_script
    assert 'repo_owned_downloads_dir="$(resolve_path_allow_missing "$REPO_ROOT/Docker/Downloads/files")"' in manifest_script
    assert 'resolved_canonical_files_dir="$(resolve_path_allow_missing "$canonical_files_dir")"' in manifest_script
    assert "realpath -m" not in manifest_script


def test_publish_download_bundle_rejects_nested_files_layout_before_manifest_sync() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert "verify_bundle_layout()" in publish_script
    assert 'echo "Bundle root points at files/ directory: $normalized_bundle_dir"' in publish_script
    assert 'local nested_files_dir="$files_dir/files"' in publish_script
    assert 'echo "Bundle is malformed: found nested files directory under $nested_files_dir"' in publish_script
    assert 'echo "Publish from the stage or bundle root, not its files/ child."' in publish_script
    assert 'verify_bundle_layout "$BUNDLE_DIR" "$FILES_SOURCE"' in publish_script
    assert publish_script.index('verify_bundle_layout "$BUNDLE_DIR" "$FILES_SOURCE"') < publish_script.index("artifacts=()")


def test_publish_download_bundle_validates_live_verify_url_before_deploy_mutation() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert "validate_absolute_http_url()" in publish_script
    assert "expected absolute http:// or https:// URL" in publish_script
    assert 'validate_absolute_http_url "$LIVE_VERIFY_TARGET" "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"' in publish_script
    assert publish_script.index('validate_absolute_http_url "$LIVE_VERIFY_TARGET" "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"') < publish_script.index('sync_source_dir="$(mktemp -d)"')
    assert publish_script.index('validate_absolute_http_url "$LIVE_VERIFY_TARGET" "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"') < publish_script.index('bash "$SCRIPT_DIR/generate-releases-manifest.sh"')


def test_publish_download_bundle_rejects_files_child_root_before_fallback_lookup() -> None:
    publish_script = (REPO_ROOT / "scripts" / "publish-download-bundle.sh").read_text(encoding="utf-8")

    assert 'echo "Bundle root points at files/ directory: $normalized_bundle_dir"' in publish_script
    assert 'verify_bundle_layout "$BUNDLE_DIR" "$FILES_SOURCE"' in publish_script
    assert publish_script.index('verify_bundle_layout "$BUNDLE_DIR" "$FILES_SOURCE"') < publish_script.index('if to_bool "$ALLOW_BUNDLE_FILES_SOURCE_FALLBACK"; then')


def test_s3_publish_rejects_files_child_root_before_layout_check() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-download-bundle-s3.sh").read_text(encoding="utf-8")

    assert 'echo "Bundle root points at files/ directory: $normalized_bundle_dir"' in publisher
    assert 'verify_bundle_layout "$BUNDLE_DIR" "$FILES_SOURCE"' in publisher
    assert publisher.index('verify_bundle_layout "$BUNDLE_DIR" "$FILES_SOURCE"') < publisher.index('echo "Expected desktop-download-bundle layout: releases.json + files/chummer-*" >&2')


def test_http_publish_rejects_files_child_root_before_manifest_checks() -> None:
    publisher = (REPO_ROOT / "scripts" / "publish-download-bundle-http.sh").read_text(encoding="utf-8")

    assert 'echo "Bundle root points at files/ directory: $normalized_bundle_dir"' in publisher
    assert 'verify_bundle_layout "$BUNDLE_DIR" "$BUNDLE_DIR/files"' in publisher
    assert publisher.index('verify_bundle_layout "$BUNDLE_DIR" "$BUNDLE_DIR/files"') < publisher.index('echo "Bundle is missing releases.json: $MANIFEST_PATH" >&2')


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

    assert 'WORKSPACE_ROOT="$(cd "$REPO_ROOT_PHYSICAL/.." && pwd -P)"' in publish_script
    assert 'ROOT_RELEASE_BLOCKERS_PATH="${CHUMMER_ROOT_RELEASE_BLOCKERS_PATH:-$WORKSPACE_ROOT/RELEASE_BLOCKERS.generated.json}"' in publish_script
    assert 'PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS="${CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS:-86400}"' in publish_script
    assert 'ROOT_RELEASE_BLOCKERS_PATH="${CHUMMER_ROOT_RELEASE_BLOCKERS_PATH:-$REPO_ROOT/../RELEASE_BLOCKERS.generated.json}"' not in publish_script
    assert "require_public_stable_root_blocker_clearance()" in publish_script
    assert 'if [[ "$normalized_release_channel" != "public_stable" ]]; then' in publish_script
    assert 'python3 - "$ROOT_RELEASE_BLOCKERS_PATH" "$PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS" <<\'PY\'' in publish_script
    assert '"release_posture:non_flagship_channel"' in publish_script
    assert "Public stable publication requires fresh root release blocker truth." in publish_script
    assert 'MAX_AGE_ENV_LABEL = "CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS"' in publish_script
    assert 'require_public_stable_root_blocker_clearance "$release_channel"' in publish_script
    assert publish_script.index('require_public_stable_root_blocker_clearance "$release_channel"') < publish_script.index('bash "$SCRIPT_DIR/generate-releases-manifest.sh"')


def test_release_build_checks_are_owned_by_local_scripts() -> None:
    assert (REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh").is_file()
    assert (REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh").is_file()
    assert (REPO_ROOT / "scripts" / "materialize_release_candidate_handoff.py").is_file()


def test_linux_desktop_exit_gate_reports_direct_host_build_failures_before_missing_host_noise() -> None:
    gate = (REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh").read_text(encoding="utf-8")

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

    assert 'DEFAULT_MACOS_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"' in gate
    assert 'RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"' in gate
    assert 'RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"' in gate
    assert 'MACOS_LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_MACOS_LOCAL_DESKTOP_FILES_ROOT"' in gate
    assert 'MACOS_LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"' in gate
    assert "Promoted macOS installer was not resolved from the release-aligned desktop shelf" in gate


def test_aggregate_desktop_materializer_defers_to_release_aligned_shelf_resolution() -> None:
    gate = (REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-executable-exit-gate.sh").read_text(encoding="utf-8")

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


def test_release_generator_binds_registry_commit_and_refreshes_localization_outside_source_tree() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert 'REGISTRY_AUTHORITY_COMMIT="${CHUMMER_HUB_REGISTRY_EXPECTED_COMMIT:-${CHUMMER_REGISTRY_COMMIT:-}}"' in generator
    assert '--registry-commit "$REGISTRY_AUTHORITY_COMMIT"' in generator
    assert 'materializer_help" != *"--registry-commit"*' in generator
    assert 'generated_output="$(mktemp "${TMPDIR:-/tmp}/chummer-ui-localization-gate.XXXXXX")"' in generator
    assert '--output "$generated_output"' in generator
    assert '--local-release-proof "$local_release_proof"' in generator
    assert 'generated_output="$ui_root/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"' not in generator


def test_scoped_release_generation_omits_retained_manifest_artifacts_from_materializer() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert (
        'if to_bool "$SCOPE_TO_STAGE_ARTIFACTS"; then\n'
        '    echo "scoped stage artifacts active; omitted incumbent manifest inputs from registry materializer" >&2\n'
        '  elif [[ -n "$manifest_override" && -f "$manifest_override" ]]; then'
    ) in generator
