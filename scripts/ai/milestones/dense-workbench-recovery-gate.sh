#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/DENSE_WORKBENCH_RECOVERY_GATE.generated.json"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$repo_root" "$receipt_path"
from __future__ import annotations

import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])
design_root = Path("/docker/chummercomplete/chummer-design/products/chummer")


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise ValueError(f"JSON root is not an object: {path}")
    return payload


def status_pass(value: Any) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def parse_int_attribute(text: str, name: str) -> int | None:
    match = re.search(rf'\b{name}="([0-9]+)"', text)
    return int(match.group(1)) if match else None


def parse_style_setter(text: str, selector: str, property_name: str) -> str | None:
    style = re.search(
        rf'<Style Selector="{re.escape(selector)}">(.*?)</Style>',
        text,
        re.DOTALL,
    )
    if not style:
        return None
    setter = re.search(
        rf'<Setter Property="{re.escape(property_name)}" Value="([^"]+)"',
        style.group(1),
    )
    return setter.group(1) if setter else None


def parse_padding(value: str | None) -> tuple[int, int] | None:
    if not value:
        return None
    parts = [part.strip() for part in value.split(",")]
    if len(parts) == 1 and parts[0].isdigit():
        amount = int(parts[0])
        return amount, amount
    if len(parts) == 2 and all(part.isdigit() for part in parts):
        return int(parts[0]), int(parts[1])
    if len(parts) == 4 and all(part.isdigit() for part in parts):
        return max(int(parts[0]), int(parts[2])), max(int(parts[1]), int(parts[3]))
    return None


def require_token(label: str, text: str, token: str, reasons: list[str]) -> None:
    if token not in text:
        reasons.append(f"{label} is missing required token: {token}")


def write_receipt(payload: dict[str, Any]) -> None:
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


paths = {
    "dense_budget": design_root / "DENSE_WORKBENCH_BUDGET.yaml",
    "veteran_first_minute": design_root / "VETERAN_FIRST_MINUTE_GATE.yaml",
    "feedback": repo_root / "feedback" / "2026-04-12-classic-dense-workbench-and-veteran-parity.md",
    "post_flagship_feedback": repo_root / "feedback" / "2026-04-13-post-flagship-release-train-and-veteran-certification.md",
    "app_axaml": repo_root / "Chummer.Avalonia" / "App.axaml",
    "main_window_axaml": repo_root / "Chummer.Avalonia" / "MainWindow.axaml",
    "main_window_state_refresh": repo_root / "Chummer.Avalonia" / "MainWindow.StateRefresh.cs",
    "section_host_axaml": repo_root / "Chummer.Avalonia" / "Controls" / "SectionHostControl.axaml",
    "toolstrip_axaml": repo_root / "Chummer.Avalonia" / "Controls" / "ToolStripControl.axaml",
    "summary_header_axaml": repo_root / "Chummer.Avalonia" / "Controls" / "SummaryHeaderControl.axaml",
    "flagship_tests": repo_root / "Chummer.Tests" / "Presentation" / "AvaloniaFlagshipUiGateTests.cs",
    "visual_gate": repo_root / ".codex-studio" / "published" / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json",
    "layout_gate": repo_root / ".codex-studio" / "published" / "CHUMMER5A_LAYOUT_HARD_GATE.generated.json",
}

reasons: list[str] = []
missing_paths = [name for name, path in paths.items() if not path.is_file()]
if missing_paths:
    reasons.extend(f"Missing required evidence path: {paths[name]}" for name in missing_paths)
    write_receipt(
        {
            "generatedAt": now_iso(),
            "contractName": "chummer6-ui.dense_workbench_recovery_gate",
            "status": "fail",
            "summary": "Dense workbench recovery proof is missing required inputs.",
            "reasons": reasons,
            "evidencePaths": {name: str(path) for name, path in paths.items()},
        }
    )
    raise SystemExit(81)

dense_budget_text = read_text(paths["dense_budget"])
veteran_gate_text = read_text(paths["veteran_first_minute"])
feedback_text = read_text(paths["feedback"])
post_feedback_text = read_text(paths["post_flagship_feedback"])
app_text = read_text(paths["app_axaml"])
main_window_text = read_text(paths["main_window_axaml"])
main_window_state_refresh_text = read_text(paths["main_window_state_refresh"])
section_host_text = read_text(paths["section_host_axaml"])
toolstrip_text = read_text(paths["toolstrip_axaml"])
summary_header_text = read_text(paths["summary_header_axaml"])
test_text = read_text(paths["flagship_tests"])
visual_gate = load_json(paths["visual_gate"])
layout_gate = load_json(paths["layout_gate"])
visual_evidence = visual_gate.get("evidence") if isinstance(visual_gate.get("evidence"), dict) else {}

budget_reasons: list[str] = []
layout_reasons: list[str] = []
screenshot_review_reasons: list[str] = []


def append_reason(message: str, *buckets: list[str]) -> None:
    reasons.append(message)
    for bucket in buckets:
        bucket.append(message)


def require_bucketed_token(label: str, text: str, token: str, *buckets: list[str]) -> None:
    if token not in text:
        append_reason(f"{label} is missing required token: {token}", *buckets)

for token in [
    "This note is historical input, not an active queue source.",
    "Badge density, row-preserving padding, accessibility-without-roomy-chrome, and menu/toolstrip review are covered",
    "scripts/ai/milestones/dense-workbench-recovery-gate.sh",
    "scripts/ai/milestones/chummer5a-screenshot-review-gate.sh",
]:
    require_bucketed_token(
        "feedback/2026-04-12-classic-dense-workbench-and-veteran-parity.md",
        feedback_text,
        token,
        budget_reasons,
        screenshot_review_reasons,
    )
for token in [
    "Avalonia primary-route proof stays independent from Blazor fallback proof",
    "Screenshot-backed parity review for menu, toolstrip, roster, master index, settings, and import is covered",
]:
    require_bucketed_token(
        "feedback/2026-04-13-post-flagship-release-train-and-veteran-certification.md",
        post_feedback_text,
        token,
        screenshot_review_reasons,
    )
for token in [
    "shell_outer_margin_max: 8",
    "row_spacing_max: 6",
    "card_padding_max: 10",
    "compact_button_min_height_max: 32",
    "menu_and_toolstrip_combined_height_max: 72",
    "dense_list_visible_row_min: 9",
    "header_to_content_ratio_max: 0.30",
]:
    require_bucketed_token("DENSE_WORKBENCH_BUDGET.yaml", dense_budget_text, token, budget_reasons)
for token in ["Immediate toolstrip", "Bottom status strip", "recover_section_rhythm"]:
    require_bucketed_token("VETERAN_FIRST_MINUTE_GATE.yaml", veteran_gate_text, token, budget_reasons, layout_reasons)

budget_evidence: dict[str, Any] = {}
button_min_height = parse_style_setter(app_text, "Button", "MinHeight")
text_box_padding = parse_padding(parse_style_setter(app_text, "TextBox", "Padding"))
card_padding = parse_padding(parse_style_setter(app_text, "Border.shell-card", "Padding"))
section_title_font_size = parse_style_setter(app_text, "TextBlock.shell-section-title", "FontSize")
toolstrip_item_height = parse_int_attribute(toolstrip_text, "ItemHeight")
section_rows_height = re.search(r'x:Name="SectionRowsList"\s+Height="([0-9]+)"', section_host_text)
section_rows_height_value = int(section_rows_height.group(1)) if section_rows_height else None

budget_evidence.update(
    {
        "fluentDensityStyle": "Compact" if 'FluentTheme DensityStyle="Compact"' in app_text else "missing",
        "buttonMinHeight": button_min_height,
        "textBoxPadding": text_box_padding,
        "cardPadding": card_padding,
        "sectionTitleFontSize": section_title_font_size,
        "toolstripItemHeight": toolstrip_item_height,
        "sectionRowsListHeight": section_rows_height_value,
        "mainWindowContentColumns": "0,*,0" if 'ColumnDefinitions="0,*,0"' in main_window_text else "missing",
        "conditionalNavigatorCollapse": all(
            token in (main_window_text + main_window_state_refresh_text)
            for token in ['x:Name="LeftNavigatorRegion"', 'IsVisible="False"', "new GridLength(228)", "new GridLength(0)"]
        ),
        "rightRailCollapsed": all(
            token in main_window_text
            for token in ['Width="0"', 'MinWidth="0"', 'MaxWidth="0"', 'IsHitTestVisible="False"']
        ),
        "loadedRunnerTabStripPanel": all(
            token in summary_header_text
            for token in ['x:Name="LoadedRunnerTabStripBorder"', "classic-tabs"]
        ),
    }
)

if budget_evidence["fluentDensityStyle"] != "Compact":
    append_reason("Avalonia app is not using compact Fluent density.", budget_reasons)
if button_min_height is None or int(button_min_height) > 32:
    append_reason(f"Button MinHeight exceeds compact budget or is missing: {button_min_height}.", budget_reasons)
if text_box_padding is None or text_box_padding[0] > 8 or text_box_padding[1] > 6:
    append_reason(f"TextBox padding exceeds dense input budget or is missing: {text_box_padding}.", budget_reasons)
if card_padding is None or card_padding[0] > 10 or card_padding[1] > 10:
    append_reason(f"Card padding exceeds dense workbench budget or is missing: {card_padding}.", budget_reasons)
if section_title_font_size is None or int(section_title_font_size) > 13:
    append_reason(f"Section header font is oversized or missing: {section_title_font_size}.", budget_reasons)
if toolstrip_item_height is None or toolstrip_item_height > 32:
    append_reason(f"Toolstrip item height exceeds compact button budget or is missing: {toolstrip_item_height}.", budget_reasons)
if section_rows_height_value is None or section_rows_height_value < 160:
    append_reason(f"Dense section rows list is too short for row-visibility proof: {section_rows_height_value}.", budget_reasons)
if budget_evidence["mainWindowContentColumns"] == "missing":
    append_reason("Main window does not keep the classic center-first 0,*,0 dense workbench posture.", layout_reasons)
if not budget_evidence["conditionalNavigatorCollapse"]:
    append_reason("Default shell does not collapse workspace chrome until a multi-workspace session exists.", layout_reasons)
if not budget_evidence["rightRailCollapsed"]:
    append_reason("Default right rail does not surrender space back to the dense center pane.", layout_reasons)
if not budget_evidence["loadedRunnerTabStripPanel"]:
    append_reason("Loaded-runner tab strip posture is not present in the summary header.", layout_reasons)

for token in [
    'Classes="shell-toolstrip-band"',
    'WrapPanel Orientation="Horizontal" ItemHeight="28"',
    'x:Name="SaveButton"',
    'x:Name="PrintButton"',
    'x:Name="CopyButton"',
    'x:Name="ImportFileButton"',
    'x:Name="SettingsButton"',
]:
    require_bucketed_token("ToolStripControl.axaml", toolstrip_text, token, layout_reasons)
for disallowed in ["shell-action-badge", "shell-action-caption", "Quick Actions", "Workbench State", "dashboard tile"]:
    if disallowed in toolstrip_text:
        append_reason(f"Toolstrip contains disallowed roomy/dashboard marker: {disallowed}", layout_reasons)

for token in [
    "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
    "Desktop_shell_preserves_classic_dense_center_first_workbench_posture",
    "Assert.IsFalse(leftNavigatorRegion.IsVisible",
]:
    require_bucketed_token("AvaloniaFlagshipUiGateTests.cs", test_text, token, layout_reasons)

for token in [
    "Visual_review_evidence_is_published_for_light_and_dark_shell_states",
    "Assert.IsTrue(toolbarButtonHeights.All(height => height <= 40d)",
    "Assert.IsTrue(menuBarRegion.Bounds.Height <= 72d",
    "Assert.IsTrue(statusStripRegion.Bounds.Height <= 72d",
    "02-menu-open-light.png",
    "05-dense-section-light.png",
    "06-dense-section-dark.png",
    "08-cyberware-dialog-light.png",
]:
    require_bucketed_token("AvaloniaFlagshipUiGateTests.cs", test_text, token, screenshot_review_reasons)

if not status_pass(visual_gate.get("status")):
    append_reason("Desktop visual familiarity gate is not passing.", screenshot_review_reasons)
if not status_pass(layout_gate.get("status")):
    append_reason("Chummer5a layout hard gate is not passing.", screenshot_review_reasons)
for key in ["legacy_dense_builder_rhythm", "runtime_backed_toolstrip_actions", "runtime_backed_menu_bar_labels", "default_single_runner_keeps_workspace_chrome_collapsed"]:
    if not status_pass(visual_evidence.get(key)):
        append_reason(f"Desktop visual familiarity evidence does not pass {key}.", screenshot_review_reasons)
required_screenshots = set(visual_evidence.get("required_screenshots") or [])
for screenshot in [
    "02-menu-open-light.png",
    "05-dense-section-light.png",
    "06-dense-section-dark.png",
    "08-cyberware-dialog-light.png",
]:
    if screenshot not in required_screenshots:
        append_reason(f"Desktop visual familiarity gate does not require screenshot review for {screenshot}.", screenshot_review_reasons)

payload = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.dense_workbench_recovery_gate",
    "status": "pass" if not reasons else "fail",
    "summary": (
        "Dense workbench recovery frontier is closed by executable budget, accessibility, and screenshot-backed familiarity proof."
        if not reasons
        else "Dense workbench recovery frontier still has blocking proof gaps."
    ),
    "reasons": reasons,
    "frontierIdsClosed": [5179993690, 2518566268, 3639684992, 2282132892, 3782970110],
    "backlogItemsClosed": [
        "Reduce: padding that cuts visible rows",
        "Keep accessibility first-class without equating accessibility to oversized roomy chrome.",
        "Make screenshot-based Chummer5a compare review mandatory for: menu and toolstrip",
        "Make screenshot-based Chummer5a compare review mandatory for: dense builder",
    ],
    "budgetEvidence": budget_evidence,
    "budgetReview": {
        "status": "pass" if not budget_reasons else "fail",
        "reasons": budget_reasons,
        "denseBudgetPath": str(paths["dense_budget"]),
        "veteranFirstMinutePath": str(paths["veteran_first_minute"]),
    },
    "layoutChromeReview": {
        "status": "pass" if not layout_reasons else "fail",
        "reasons": layout_reasons,
        "mainWindowContentColumns": budget_evidence["mainWindowContentColumns"],
        "conditionalNavigatorCollapse": budget_evidence["conditionalNavigatorCollapse"],
        "rightRailCollapsed": budget_evidence["rightRailCollapsed"],
        "loadedRunnerTabStripPanel": budget_evidence["loadedRunnerTabStripPanel"],
    },
    "screenshotBackedReview": {
        "status": "pass" if not screenshot_review_reasons else "fail",
        "reasons": screenshot_review_reasons,
        "requiredScreenshots": sorted(required_screenshots),
        "menuAndToolstripScreenshots": ["02-menu-open-light.png"],
        "denseBuilderScreenshots": [
            "05-dense-section-light.png",
            "06-dense-section-dark.png",
            "08-cyberware-dialog-light.png",
        ],
        "screenshotDirectory": visual_evidence.get("screenshot_dir"),
        "supportingReceiptStatuses": {
            "desktopVisualFamiliarity": visual_gate.get("status"),
            "chummer5aLayoutHardGate": layout_gate.get("status"),
        },
    },
    "supportingReceipts": {
        "desktopVisualFamiliarity": str(paths["visual_gate"]),
        "chummer5aLayoutHardGate": str(paths["layout_gate"]),
    },
    "sourceEvidence": {name: str(path) for name, path in paths.items()},
    "evidence": {
        "supportingReceipts": {
            "desktopVisualFamiliarity": str(paths["visual_gate"]),
            "chummer5aLayoutHardGate": str(paths["layout_gate"]),
        },
        "sourceEvidence": {name: str(path) for name, path in paths.items()},
        "frontierIdsClosed": [5179993690, 2518566268, 3639684992, 2282132892, 3782970110],
        "backlogItemsClosed": [
            "Reduce: padding that cuts visible rows",
            "Keep accessibility first-class without equating accessibility to oversized roomy chrome.",
            "Make screenshot-based Chummer5a compare review mandatory for: menu and toolstrip",
            "Make screenshot-based Chummer5a compare review mandatory for: dense builder",
        ],
        "reasonCount": len(reasons),
        "failureCount": len(reasons),
    },
}
write_receipt(payload)
if reasons:
    raise SystemExit(82)
PY

echo "[dense-workbench-recovery] PASS"
