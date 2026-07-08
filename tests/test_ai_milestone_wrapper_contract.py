from pathlib import Path


def test_gm_screen_export_staged_proof_wrapper_uses_alias_safe_repo_root() -> None:
    script = Path("scripts/ai/milestones/blazor-workbench-gm-screen-export-staged-proof-check.sh").read_text(
        encoding="utf-8",
    )

    assert 'script_dir_physical="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"' in script
    assert 'repo_root_physical="$(cd "$script_dir_physical/../../.." && pwd -P)"' in script
    assert 'repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-$repo_root_physical}"' in script
    assert 'repo_root="$repo_root_physical"' in script
    assert 'repo_root="$(cd -L "$repo_root_alias_candidate" && pwd -L)"' in script
    assert 'python3 "$repo_root/scripts/materialize-blazor-workbench-gm-screen-export-staged-proof.py"' in script
    assert 'script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"' not in script
    assert 'repo_root="$(cd "$script_dir/../../.." && pwd)"' not in script
