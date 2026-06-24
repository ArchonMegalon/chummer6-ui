from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
MATERIALIZER = REPO_ROOT / "scripts" / "ai" / "milestones" / "materialize-desktop-executable-exit-gate.sh"
VERIFY_SCRIPT = REPO_ROOT / "scripts" / "ai" / "verify.sh"


def test_desktop_executable_exit_gate_materializer_exposes_external_blocking_mode_contract() -> None:
    text = MATERIALIZER.read_text(encoding="utf-8")
    assert '"blockingMode": blocking_mode' in text
    assert '"blocking_mode": blocking_mode' in text
    assert '"blockedByExternalConstraintsOnly": blocked_by_external_constraints_only' in text
    assert '"blocked_by_external_constraints_only": blocked_by_external_constraints_only' in text
    assert "Desktop executable exit gate is blocked only by external execution constraints." in text
    assert '"external_only"' in text
    assert '"mixed_or_local"' in text
    assert "windows installer visual proof is missing; capture progress and completion screenshots on a windows host" in text


def test_shared_verify_lane_checks_desktop_executable_exit_gate_blocking_aliases() -> None:
    text = VERIFY_SCRIPT.read_text(encoding="utf-8")
    assert "blockedByExternalConstraintsOnly" in text
    assert "blocked_by_external_constraints_only" in text
    assert "blockingMode" in text
    assert "blocking_mode" in text
