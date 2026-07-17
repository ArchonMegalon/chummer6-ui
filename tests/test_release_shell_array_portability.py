from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]

HELPER_SNIPPETS = (
    "array_count()",
    'local restore_nounset=0',
    'case "$-" in',
    'set +u',
    'eval "set -- \\"\\${${array_name}[@]}\\""',
    'local count="$#"',
    'set -u',
)

SCRIPT_EXPECTATIONS = {
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
            'if (( live_downloads_mirror_dir_count > 0 )); then',
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


def test_ui_release_shell_scripts_use_nounset_safe_array_count() -> None:
    for script_path, expectations in SCRIPT_EXPECTATIONS.items():
        text = script_path.read_text(encoding="utf-8")

        for snippet in HELPER_SNIPPETS:
            assert snippet in text, f"missing nounset-safe array_count helper snippet in {script_path}: {snippet}"
        assert 'eval "set -- \\${${array_name}[@]+\\"\\${${array_name}[@]}\\"}"' not in text

        for snippet in expectations["required"]:
            assert snippet in text, f"missing expected portability usage in {script_path}: {snippet}"

        for snippet in expectations["forbidden"]:
            assert snippet not in text, f"found bash3-unsafe raw array length expansion in {script_path}: {snippet}"
