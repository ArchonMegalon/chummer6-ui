from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
MATERIALIZER = REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-executable-exit-gate.sh"
VISUAL_GATE = REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-visual-familiarity-exit-gate.sh"
FLAGSHIP_GATE = REPO_ROOT / "scripts" / "ai" / "milestones" / "b14-flagship-ui-release-gate.sh"
VERIFY_SCRIPT = REPO_ROOT / "scripts" / "ai" / "verify.sh"
VISUAL_FAMILIARITY_GATE = REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-visual-familiarity-exit-gate.sh"
WORKFLOW_EXECUTION_GATE = REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-workflow-execution-gate.sh"
CHUMMER5A_PARITY_TESTER = REPO_ROOT / "scripts" / "ai" / "milestones" / "chummer5a-ultimate-parity-tester.sh"
SR4_WORKFLOW_PARITY = REPO_ROOT / "scripts" / "ai" / "milestones" / "sr4-desktop-workflow-parity-check.sh"
SR6_WORKFLOW_PARITY = REPO_ROOT / "scripts" / "ai" / "milestones" / "sr6-desktop-workflow-parity-check.sh"
LINUX_DESKTOP_EXIT_GATE = REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh"
WINDOWS_DESKTOP_EXIT_GATE = REPO_ROOT / "scripts" / "materialize-windows-desktop-exit-gate.sh"
MACOS_DESKTOP_EXIT_GATE = REPO_ROOT / "scripts" / "materialize-macos-desktop-exit-gate.sh"
MOUSE_FIRST_JOURNEY_MATRIX = REPO_ROOT / "scripts" / "run-desktop-mouse-first-journey-matrix.sh"


def test_desktop_executable_exit_gate_materializer_exposes_external_blocking_mode_contract() -> None:
    text = MATERIALIZER.read_text(encoding="utf-8")
    assert '"blockingMode": blocking_mode' in text
    assert '"blocking_mode": blocking_mode' in text
    assert '"blockedByExternalConstraintsOnly": blocked_by_external_constraints_only' in text
    assert '"blocked_by_external_constraints_only": blocked_by_external_constraints_only' in text
    assert "Desktop executable exit gate is blocked only by external execution constraints." in text
    assert '"external_only"' in text
    assert '"mixed_or_local"' in text
    assert "missing_windows_visual_proof_capture" in text
    assert "windows installer visual proof is missing; capture progress and completion screenshots on a windows host" in text
    assert "windows installer visual proof must be captured on a windows host before promotion can pass" in text


def test_shared_verify_lane_checks_desktop_executable_exit_gate_blocking_aliases() -> None:
    text = VERIFY_SCRIPT.read_text(encoding="utf-8")
    assert "blockedByExternalConstraintsOnly" in text
    assert "blocked_by_external_constraints_only" in text
    assert "blockingMode" in text
    assert "blocking_mode" in text


def test_release_gate_scripts_prefer_repo_local_portal_release_channel_fallback() -> None:
    expected = 'presentation_release_channel_path="$repo_root/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json"'
    forbidden = 'presentation_release_channel_path="/docker/chummercomplete/chummer-presentation/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json"'

    for script_path in (MATERIALIZER, VISUAL_GATE, FLAGSHIP_GATE):
        text = script_path.read_text(encoding="utf-8")
        assert expected in text
        assert forbidden not in text


def test_desktop_gate_scripts_default_repo_aliases_to_the_current_checkout() -> None:
    materializer_text = MATERIALIZER.read_text(encoding="utf-8")
    visual_gate_text = VISUAL_GATE.read_text(encoding="utf-8")
    flagship_gate_text = FLAGSHIP_GATE.read_text(encoding="utf-8")
    linux_gate_text = LINUX_DESKTOP_EXIT_GATE.read_text(encoding="utf-8")
    windows_gate_text = WINDOWS_DESKTOP_EXIT_GATE.read_text(encoding="utf-8")
    macos_gate_text = MACOS_DESKTOP_EXIT_GATE.read_text(encoding="utf-8")

    assert 'repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-$repo_root_physical}"' in materializer_text
    assert 'repo_root_value = str(globals().get("repo_root") or Path.cwd())' in materializer_text
    assert 'repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-$repo_root_physical}"' in visual_gate_text
    assert 'repo_root_alias_candidate="${CHUMMER_UI_REPO_ROOT_ALIAS:-$repo_root_physical}"' in flagship_gate_text
    assert 'REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"' in linux_gate_text
    assert 'REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"' in windows_gate_text
    assert 'REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"' in macos_gate_text
    assert '/docker/chummercomplete/chummer6-ui' not in materializer_text
    assert '/docker/chummercomplete/chummer6-ui' not in visual_gate_text
    assert '/docker/chummercomplete/chummer6-ui' not in flagship_gate_text
    assert '/docker/chummercomplete/chummer6-ui' not in linux_gate_text
    assert '/docker/chummercomplete/chummer6-ui' not in windows_gate_text
    assert '/docker/chummercomplete/chummer6-ui' not in macos_gate_text


def test_desktop_exit_gate_scripts_avoid_bash4_mapfile_collectors() -> None:
    for script_path in (MACOS_DESKTOP_EXIT_GATE, LINUX_DESKTOP_EXIT_GATE, WINDOWS_DESKTOP_EXIT_GATE):
        text = script_path.read_text(encoding="utf-8")

        assert 'RELEASE_PROMOTED_TUPLE=()' in text, f"missing tuple initializer in {script_path}"
        assert 'while IFS= read -r tuple_value; do' in text, f"missing bash3-safe tuple collector loop in {script_path}"
        assert 'RELEASE_PROMOTED_TUPLE+=("$tuple_value")' in text, f"missing tuple append in {script_path}"
        assert 'mapfile -t RELEASE_PROMOTED_TUPLE' not in text, f"exit gate must not rely on bash4 mapfile in {script_path}"


def test_release_gate_milestone_scripts_avoid_bash4_mapfile_collectors() -> None:
    expected_scripts = {
        VISUAL_FAMILIARITY_GATE: (
            'runtime_screenshot_candidate_dirs=()',
            'while IFS= read -r runtime_screenshot_candidate_dir; do',
            'runtime_screenshot_candidate_dirs+=("$runtime_screenshot_candidate_dir")',
        ),
        WORKFLOW_EXECUTION_GATE: (
            'dependency_refresh_env=()',
            'while IFS= read -r dependency_refresh_env_var; do',
            'dependency_refresh_env+=("$dependency_refresh_env_var")',
        ),
        CHUMMER5A_PARITY_TESTER: (
            'fixtures=()',
            "while IFS= read -r -d '' fixture_path; do",
            'fixtures+=("$fixture_path")',
        ),
    }

    for script_path, expected_snippets in expected_scripts.items():
        text = script_path.read_text(encoding="utf-8")
        for snippet in expected_snippets:
            assert snippet in text, f"missing bash3-safe collector snippet in {script_path}: {snippet}"
        assert "mapfile -t" not in text, f"release-gate milestone script must not rely on bash4 mapfile in {script_path}"


def test_desktop_executable_exit_gate_avoids_bash4_case_conversion_for_tuple_receipt_paths() -> None:
    text = MATERIALIZER.read_text(encoding="utf-8")

    assert "upper_ascii()" in text
    assert "printf '%s' \"${1:-}\" | tr '[:lower:]' '[:upper:]'" in text
    assert 'head_token="$(upper_ascii "$head")"' in text
    assert 'rid_token="$(upper_ascii "$rid")"' in text
    assert 'linux_gate_tuple_path="$repo_root/.codex-studio/published/UI_LINUX_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json"' in text
    assert 'windows_gate_tuple_path="$repo_root/.codex-studio/published/UI_WINDOWS_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json"' in text
    assert 'macos_gate_tuple_path="$repo_root/.codex-studio/published/UI_MACOS_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json"' in text
    assert "${head^^}" not in text
    assert "${rid^^}" not in text


def test_linux_desktop_exit_gate_avoids_bash4_associative_arrays_for_run_retention() -> None:
    text = LINUX_DESKTOP_EXIT_GATE.read_text(encoding="utf-8")

    assert 'keep_roots_file="$(mktemp "${TMPDIR:-/tmp}/chummer-linux-exit-keep-roots.XXXXXX")" || return 1' in text
    assert 'printf \'%s\\n\' "$current_run_root" >> "$keep_roots_file"' in text
    assert 'grep -Fqx -- "$resolved_path" "$keep_roots_file"' in text
    assert 'rm -f "$keep_roots_file"' in text
    assert "declare -A keep_roots=()" not in text


def test_mouse_first_journey_matrix_runner_uses_alias_safe_repo_root_contract() -> None:
    text = MOUSE_FIRST_JOURNEY_MATRIX.read_text(encoding="utf-8")

    assert 'SCRIPT_DIR_PHYSICAL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"' in text
    assert 'REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR_PHYSICAL/.." && pwd -P)"' in text
    assert 'REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"' in text
    assert 'REPO_ROOT="$REPO_ROOT_PHYSICAL"' in text
    assert 'SCRIPT_DIR="$REPO_ROOT/scripts"' in text
    assert 'OUTPUT_ROOT="${2:-$REPO_ROOT/dist/mouse-first-journey-matrix}"' in text
    assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"' not in text
    assert 'REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"' not in text


def test_workflow_family_parity_wrappers_fallback_when_flock_is_unavailable() -> None:
    for script_path in (SR4_WORKFLOW_PARITY, SR6_WORKFLOW_PARITY):
        text = script_path.read_text(encoding="utf-8")

        assert "if command -v flock >/dev/null 2>&1; then" in text
        assert 'exec 9>"$workflow_family_chain_lock_path"' in text
        assert "flock 9" in text
        assert 'workflow_family_chain_lock_dir="$workflow_family_chain_lock_path.d"' in text
        assert 'workflow_family_chain_lock_pid_path="$workflow_family_chain_lock_dir/pid"' in text
        assert 'if mkdir "$workflow_family_chain_lock_dir" 2>/dev/null; then' in text
        assert "printf '%s\\n' \"$$\" > \"$workflow_family_chain_lock_pid_path\"" in text
        assert 'trap \'release_workflow_family_chain_lock\' EXIT' in text
        assert 'if [[ -n "$owner_pid" ]] && ! kill -0 "$owner_pid" 2>/dev/null; then' in text
