#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/CLASSIC_DENSE_WORKBENCH_POSTURE_GATE.generated.json"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$receipt_path"
from __future__ import annotations

import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def style_value(text: str, selector: str, property_name: str) -> str | None:
    match = re.search(
        rf'<Style Selector="{re.escape(selector)}">(.*?)</Style>',
        text,
        flags=re.DOTALL,
    )
    if not match:
        return None
    setter = re.search(
        rf'<Setter Property="{re.escape(property_name)}" Value="([^"]+)"',
        match.group(1),
    )
    return setter.group(1) if setter else None


def numeric_style(text: str, selector: str, property_name: str) -> float | None:
    value = style_value(text, selector, property_name)
    if value is None:
        return None
    try:
        return float(value)
    except ValueError:
        return None


receipt_path = Path(sys.argv[1])
app_text = read("Chummer.Avalonia/App.axaml")
main_window_text = read("Chummer.Avalonia/MainWindow.axaml")
section_host_text = read("Chummer.Avalonia/Controls/SectionHostControl.axaml")
toolstrip_text = read("Chummer.Avalonia/Controls/ToolStripControl.axaml")
test_text = read("Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs")
feedback_text = read("feedback/2026-04-12-classic-dense-workbench-and-veteran-parity.md")

reasons: list[str] = []
evidence: dict[str, object] = {}

frontier_ids = [1524553407, 3384607072, 1935838393, 3409035241]
frontier_tasks = [
    "Make classic dense workbench posture the default for the flagship head.",
    "Reduce: oversized section headers",
    "Reduce: nested cards around forms",
    "Reduce: decorative dashboard chrome",
]

closed_feedback_markers = [
    "This note is historical input, not an active queue source.",
    "false-complete recovery pass closed the work through repo-local executable",
    "WL-220",
    "Classic dense workbench posture is covered by `scripts/ai/milestones/classic-dense-workbench-posture-gate.sh`",
]
if not all(marker in feedback_text for marker in closed_feedback_markers):
    for task in frontier_tasks:
        feedback_marker = task.replace("Reduce: ", "")
        if task not in feedback_text and feedback_marker not in feedback_text:
            reasons.append(f"Feedback source no longer contains expected frontier task or closed-evidence marker: {task}")

if 'FluentTheme DensityStyle="Compact"' not in app_text:
    reasons.append("Avalonia is not using Fluent compact density.")

numeric_limits = [
    ("Window", "FontSize", 13),
    ("TextBlock.shell-title", "FontSize", 24),
    ("TextBlock.shell-section-title", "FontSize", 12),
    ("TextBlock.shell-metric-value", "FontSize", 16),
    ("Button.menu-button", "MinHeight", 24),
]
numeric_evidence: dict[str, float | None] = {}
for selector, property_name, maximum in numeric_limits:
    value = numeric_style(app_text, selector, property_name)
    numeric_evidence[f"{selector}.{property_name}"] = value
    if value is None or value > maximum:
        reasons.append(f"{selector} {property_name} must be <= {maximum}; found {value!r}.")

if style_value(app_text, "ListBoxItem", "Padding") != "3,1":
    reasons.append("Dense rows must use ListBoxItem padding 3,1.")
if style_value(app_text, "Button", "Padding") != "5,1":
    reasons.append("Dense toolbar buttons must use Button padding 5,1.")

if 'ColumnDefinitions="228,*,0"' not in main_window_text:
    reasons.append("Flagship workbench must keep the narrow-left / dominant-center / collapsed-right desktop layout.")
if 'x:Name="WorkspaceStripRegion"' in main_window_text:
    reasons.append("Default flagship workbench must not restore a decorative workspace-strip row.")
right_shell_index = main_window_text.find('x:Name="RightShellRegion"')
right_shell = main_window_text[right_shell_index:right_shell_index + 500] if right_shell_index >= 0 else ""
for token in ['Width="0"', 'MaxWidth="0"', 'Opacity="0"', 'IsHitTestVisible="False"']:
    if token not in right_shell:
        reasons.append(f"Collapsed right shell is missing token {token}.")

if 'Classes="shell-card"' in section_host_text:
    reasons.append("Section host must use flat shell panels, not card styling, for form/workbench surfaces.")

for forbidden in ["dashboard", "mainframe", "control center"]:
    if forbidden in (main_window_text + section_host_text + toolstrip_text).lower():
        reasons.append(f"Decorative shell copy is still present in Avalonia workbench XAML: {forbidden}.")

required_tests = [
    "Desktop_shell_preserves_classic_dense_three_pane_workbench_posture",
    "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
    "Runtime_backed_shell_avoids_modern_dashboard_copy_that_breaks_chummer5a_orientation",
    "Character_creation_preserves_familiar_dense_builder_rhythm",
]
missing_tests = [name for name in required_tests if name not in test_text]
if missing_tests:
    reasons.append("Missing dense workbench regression tests: " + ", ".join(missing_tests))

evidence.update(
    {
        "frontierIdsClosed": frontier_ids,
        "frontierTasksClosed": frontier_tasks,
        "feedbackClosedEvidenceMarkers": closed_feedback_markers,
        "numericDensityTokens": numeric_evidence,
        "listBoxItemPadding": style_value(app_text, "ListBoxItem", "Padding"),
        "buttonPadding": style_value(app_text, "Button", "Padding"),
        "usesCompactFluentDensity": 'FluentTheme DensityStyle="Compact"' in app_text,
        "usesFlatSectionPanels": 'Classes="shell-card"' not in section_host_text,
        "mainWindowLayout": "ColumnDefinitions=\"228,*,0\"" if 'ColumnDefinitions="228,*,0"' in main_window_text else "missing",
        "requiredTests": required_tests,
    }
)

payload = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.classic_dense_workbench_posture_gate",
    "status": "pass" if not reasons else "fail",
    "summary": (
        "Classic dense Avalonia workbench posture is default and the first false-complete frontier slice is closed."
        if not reasons
        else "Classic dense Avalonia workbench posture is still missing required closure evidence."
    ),
    "frontierIdsClosed": frontier_ids,
    "reasons": reasons,
    "evidence": evidence,
}
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if reasons:
    raise SystemExit(63)
PY

echo "[classic-dense-workbench-posture-gate] PASS"
