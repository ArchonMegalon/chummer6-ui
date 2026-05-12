#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_STARTUP_WORKBENCH_SURVIVAL_RECEIPT_PATH:-$repo_root/.codex-studio/published/STARTUP_WORKBENCH_SURVIVAL.generated.json}"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$repo_root" "$receipt_path"
from __future__ import annotations

import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])

TEST_MARKERS = [
    "Fresh_launch_main_window_survives_first_paint_without_self_termination",
    "Avalonia_startup_keeps_the_workbench_as_first_paint_but_still_invokes_restore_continuation_when_needed",
    "Desktop_home_window_no_longer_forces_a_dashboard_detour_for_empty_workspace_state",
    "ShouldShowOnStartup_keeps_first_launch_on_real_workbench_when_no_follow_through_is_needed",
    "Fresh_launch_workbench_does_not_render_a_fake_empty_section_expander",
]

FILTER = (
    "Name~Fresh_launch_main_window_survives_first_paint_without_self_termination"
    "|Name~Avalonia_startup_keeps_the_workbench_as_first_paint_but_still_invokes_restore_continuation_when_needed"
    "|Name~Desktop_home_window_no_longer_forces_a_dashboard_detour_for_empty_workspace_state"
    "|Name~ShouldShowOnStartup_keeps_first_launch_on_real_workbench_when_no_follow_through_is_needed"
    "|Name~Fresh_launch_workbench_does_not_render_a_fake_empty_section_expander"
)

GATE_TESTS_PATH = repo_root / "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
HOME_STARTUP_TESTS_PATH = repo_root / "Chummer.Tests/Presentation/DesktopHomeWindowStartupTests.cs"
APP_PATH = repo_root / "Chummer.Avalonia/App.axaml.cs"
PROJECTOR_PATH = repo_root / "Chummer.Avalonia/MainWindow.ShellFrameProjector.cs"


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def tail_lines(text: str, count: int = 40) -> str:
    lines = [line.rstrip() for line in text.splitlines() if line.strip()]
    return "\n".join(lines[-count:])


reasons: list[str] = []
evidence: dict[str, Any] = {
    "receiptPath": str(receipt_path),
    "sourcePaths": {
        "avaloniaGateTests": str(GATE_TESTS_PATH.relative_to(repo_root)),
        "desktopHomeStartupTests": str(HOME_STARTUP_TESTS_PATH.relative_to(repo_root)),
        "app": str(APP_PATH.relative_to(repo_root)),
        "projector": str(PROJECTOR_PATH.relative_to(repo_root)),
    },
}

for path in [GATE_TESTS_PATH, HOME_STARTUP_TESTS_PATH, APP_PATH, PROJECTOR_PATH]:
    if not path.is_file():
        reasons.append(f"Missing required startup-survival source path: {path}")

if not reasons:
    gate_tests_text = GATE_TESTS_PATH.read_text(encoding="utf-8-sig")
    home_startup_tests_text = HOME_STARTUP_TESTS_PATH.read_text(encoding="utf-8-sig")
    app_text = APP_PATH.read_text(encoding="utf-8-sig")
    projector_text = PROJECTOR_PATH.read_text(encoding="utf-8-sig")

    evidence["testMarkers"] = {
        marker: (marker in gate_tests_text or marker in home_startup_tests_text)
        for marker in TEST_MARKERS
    }
    evidence["sourceMarkers"] = {
        "DesktopInstallLinkingWindow.ShowIfNeededAsync(owner, installLinkingContext);": "DesktopInstallLinkingWindow.ShowIfNeededAsync(owner, installLinkingContext);" in app_text,
        "ShowNavigatorPane: true": "ShowNavigatorPane: true" in projector_text,
        "DesktopHomeWindow.ShowIfNeededAsync(owner, \"avalonia\", installContext: null);": "DesktopHomeWindow.ShowIfNeededAsync(owner, \"avalonia\", installContext: null);" in app_text,
    }

    missing_test_markers = [name for name, found in evidence["testMarkers"].items() if not found]
    if missing_test_markers:
        reasons.append("Startup survival gate is missing required test markers: " + ", ".join(missing_test_markers))
    if not evidence["sourceMarkers"]["DesktopInstallLinkingWindow.ShowIfNeededAsync(owner, installLinkingContext);"]:
        reasons.append("Avalonia startup path lost install-linking continuation handling.")
    if not evidence["sourceMarkers"]["ShowNavigatorPane: true"]:
        reasons.append("Avalonia startup path no longer projects the Codex navigator pane.")
    if evidence["sourceMarkers"]["DesktopHomeWindow.ShowIfNeededAsync(owner, \"avalonia\", installContext: null);"]:
        reasons.append("Avalonia startup path still reopens DesktopHomeWindow by default.")

if not reasons:
    restore_command = [
        "dotnet",
        "restore",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--ignore-failed-sources",
        "-p:NuGetAudit=false",
    ]
    build_command = [
        "dotnet",
        "build",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--configuration",
        "Debug",
        "--no-restore",
        "--nologo",
        "-m:1",
        "--verbosity",
        "quiet",
        "--ignore-failed-sources",
        "-p:NuGetAudit=false",
    ]
    test_command = [
        "dotnet",
        "test",
        "--project",
        "Chummer.Tests/Chummer.Tests.csproj",
        "--configuration",
        "Debug",
        "--no-build",
        "--no-restore",
        "--filter",
        FILTER,
        "--verbosity",
        "minimal",
    ]
    evidence["restoreCommand"] = restore_command
    evidence["buildCommand"] = build_command
    evidence["testCommand"] = test_command

    restore = subprocess.run(restore_command, cwd=repo_root, text=True, capture_output=True)
    evidence["restoreExitCode"] = restore.returncode
    evidence["restoreOutputTail"] = tail_lines((restore.stdout or "") + "\n" + (restore.stderr or ""))
    if restore.returncode != 0:
        reasons.append(f"Startup survival restore failed with exit code {restore.returncode}.")
    else:
        build = subprocess.run(build_command, cwd=repo_root, text=True, capture_output=True)
        evidence["buildExitCode"] = build.returncode
        evidence["buildOutputTail"] = tail_lines((build.stdout or "") + "\n" + (build.stderr or ""))
        if build.returncode != 0:
            reasons.append(f"Startup survival build failed with exit code {build.returncode}.")
        else:
            test = subprocess.run(test_command, cwd=repo_root, text=True, capture_output=True)
            combined = (test.stdout or "") + "\n" + (test.stderr or "")
            evidence["testExitCode"] = test.returncode
            evidence["testOutputTail"] = tail_lines(combined)
            if test.returncode != 0:
                reasons.append(f"Startup survival test slice failed with exit code {test.returncode}.")

payload = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.startup_workbench_survival",
    "status": "pass" if not reasons else "fail",
    "summary": (
        "Avalonia startup survives first paint and stays on the workbench by default."
        if not reasons
        else "Avalonia startup survival or first-paint workbench proof is incomplete."
    ),
    "reasons": reasons,
    "evidence": {
        **evidence,
        "failureCount": len(reasons),
        "reasonCount": len(reasons),
    },
}

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if reasons:
    raise SystemExit(47)
PY

echo "[startup-workbench-survival] PASS: startup stays on the workbench and survives first paint."
echo "[startup-workbench-survival] evidence: $receipt_path"
