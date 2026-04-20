#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_LAYOUT_HARD_GATE.generated.json"
legacy_frmcareer_designer_path="${CHUMMER5A_LEGACY_FRMCAREER_DESIGNER_PATH:-/docker/chummer5a/Chummer/Forms/Character Forms/CharacterCareer.Designer.cs}"
canonical_presentation_root="${CHUMMER_CANONICAL_PRESENTATION_ROOT:-/docker/chummercomplete/chummer-presentation}"
menu_bar_axaml_path="$repo_root/Chummer.Avalonia/Controls/ShellMenuBarControl.axaml"
toolstrip_axaml_path="$repo_root/Chummer.Avalonia/Controls/ToolStripControl.axaml"
toolstrip_codebehind_path="$repo_root/Chummer.Avalonia/Controls/ToolStripControl.axaml.cs"
section_host_axaml_path="$repo_root/Chummer.Avalonia/Controls/SectionHostControl.axaml"
main_window_axaml_path="$repo_root/Chummer.Avalonia/MainWindow.axaml"
main_window_state_refresh_path="$repo_root/Chummer.Avalonia/MainWindow.StateRefresh.cs"
main_window_event_handlers_path="$repo_root/Chummer.Avalonia/MainWindow.EventHandlers.cs"
main_window_control_binding_path="$repo_root/Chummer.Avalonia/MainWindow.ControlBinding.cs"
avalonia_projector_path="$repo_root/Chummer.Avalonia/MainWindow.ShellFrameProjector.cs"
app_axaml_codebehind_path="$repo_root/Chummer.Avalonia/App.axaml.cs"
if [[ -d "$canonical_presentation_root/Chummer.Blazor" ]]; then
  blazor_root="$canonical_presentation_root/Chummer.Blazor"
else
  blazor_root="$repo_root/Chummer.Blazor"
fi
blazor_shell_markup_path="$blazor_root/Components/Layout/DesktopShell.razor"
blazor_shell_path="$blazor_root/Components/Layout/DesktopShell.razor.cs"
blazor_css_path="$blazor_root/wwwroot/app.css"
resolver_path="$repo_root/Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs"
avalonia_project_path="$repo_root/Chummer.Avalonia/Chummer.Avalonia.csproj"
blazor_desktop_project_path="$repo_root/Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj"
release_gate_path="$repo_root/scripts/ai/milestones/b14-flagship-ui-release-gate.sh"
visual_gate_path="$repo_root/scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh"
ui_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
desktop_shell_ruleset_tests_path="$repo_root/Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs"

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' \
  "$receipt_path" \
  "$legacy_frmcareer_designer_path" \
  "$menu_bar_axaml_path" \
  "$toolstrip_axaml_path" \
  "$toolstrip_codebehind_path" \
  "$section_host_axaml_path" \
  "$main_window_axaml_path" \
  "$main_window_state_refresh_path" \
  "$main_window_event_handlers_path" \
  "$main_window_control_binding_path" \
  "$avalonia_projector_path" \
  "$app_axaml_codebehind_path" \
  "$blazor_shell_markup_path" \
  "$blazor_shell_path" \
  "$blazor_css_path" \
  "$resolver_path" \
  "$avalonia_project_path" \
  "$blazor_desktop_project_path" \
  "$release_gate_path" \
  "$visual_gate_path" \
  "$ui_gate_tests_path" \
  "$desktop_shell_ruleset_tests_path"
from __future__ import annotations

import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def write_receipt(path: Path, payload: dict[str, object]) -> None:
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


(
    receipt_path,
    legacy_frmcareer_designer_path,
    menu_bar_axaml_path,
    toolstrip_axaml_path,
    toolstrip_codebehind_path,
    section_host_axaml_path,
    main_window_axaml_path,
    main_window_state_refresh_path,
    main_window_event_handlers_path,
    main_window_control_binding_path,
    avalonia_projector_path,
    app_axaml_codebehind_path,
    blazor_shell_markup_path,
    blazor_shell_path,
    blazor_css_path,
    resolver_path,
    avalonia_project_path,
    blazor_desktop_project_path,
    release_gate_path,
    visual_gate_path,
    ui_gate_tests_path,
    desktop_shell_ruleset_tests_path,
) = [Path(value) for value in sys.argv[1:23]]

reasons: list[str] = []
evidence: dict[str, object] = {}

required_paths = [
    receipt_path.parent,
    legacy_frmcareer_designer_path,
    menu_bar_axaml_path,
    toolstrip_axaml_path,
    toolstrip_codebehind_path,
    section_host_axaml_path,
    main_window_axaml_path,
    main_window_state_refresh_path,
    main_window_event_handlers_path,
    main_window_control_binding_path,
    avalonia_projector_path,
    app_axaml_codebehind_path,
    blazor_shell_markup_path,
    blazor_shell_path,
    blazor_css_path,
    resolver_path,
    avalonia_project_path,
    blazor_desktop_project_path,
    release_gate_path,
    visual_gate_path,
    ui_gate_tests_path,
    desktop_shell_ruleset_tests_path,
]

missing_paths = [str(path) for path in required_paths[1:] if not path.is_file()]
if missing_paths:
    payload = {
        "generatedAt": now_iso(),
        "contract_name": "chummer6-ui.chummer5a_layout_hard_gate",
        "status": "fail",
        "summary": "Chummer5a layout hard gate is missing required source files.",
        "reasons": [f"Missing required source path: {path}" for path in missing_paths],
        "evidence": {"missingPaths": missing_paths},
    }
    write_receipt(receipt_path, payload)
    raise SystemExit(61)

legacy_text = read_text(legacy_frmcareer_designer_path)
menu_bar_text = read_text(menu_bar_axaml_path)
toolstrip_text = read_text(toolstrip_axaml_path)
toolstrip_codebehind_text = read_text(toolstrip_codebehind_path)
section_host_text = read_text(section_host_axaml_path)
main_window_text = read_text(main_window_axaml_path)
main_window_state_refresh_text = read_text(main_window_state_refresh_path)
main_window_event_handlers_text = read_text(main_window_event_handlers_path)
main_window_control_binding_text = read_text(main_window_control_binding_path)
avalonia_projector_text = read_text(avalonia_projector_path)
app_text = read_text(app_axaml_codebehind_path)
blazor_shell_markup_text = read_text(blazor_shell_markup_path)
blazor_shell_text = read_text(blazor_shell_path)
blazor_css_text = read_text(blazor_css_path)
resolver_text = read_text(resolver_path)
avalonia_project_text = read_text(avalonia_project_path)
blazor_desktop_project_text = read_text(blazor_desktop_project_path)
release_gate_text = read_text(release_gate_path)
visual_gate_text = read_text(visual_gate_path)
ui_gate_tests_text = read_text(ui_gate_tests_path)
desktop_shell_ruleset_tests_text = read_text(desktop_shell_ruleset_tests_path)

legacy_menu_markers = ["mnuCreateFile", "mnuCreateEdit", "mnuCreateSpecial"]
legacy_toolbar_markers = ["tsMain.Items.AddRange", "tsbSave", "tsbPrint", "tsbCopy"]
legacy_tab_count = len(re.findall(r"tabCharacterTabs\.Controls\.Add\(", legacy_text))

if not all(marker in legacy_text for marker in legacy_menu_markers):
    reasons.append("Legacy frmCareer designer does not expose the expected File/Edit/Special menu anchors.")
if not all(marker in legacy_text for marker in legacy_toolbar_markers):
    reasons.append("Legacy frmCareer designer does not expose the expected save/print/copy toolbar anchors.")
if legacy_tab_count < 19:
    reasons.append(f"Legacy frmCareer designer tab count is unexpectedly low: {legacy_tab_count}.")

menu_headers = re.findall(r'Header="([^"]+)"', menu_bar_text)
expected_menu_headers = ["File", "Edit", "Special", "Tools", "Windows", "Help"]
if menu_headers != expected_menu_headers:
    reasons.append(
        "Avalonia shell menu order diverges from the classic desktop contract: "
        + " -> ".join(menu_headers or ["<none>"])
    )

button_names = [
    "SaveButton",
    "PrintButton",
    "CopyButton",
    "DesktopHomeButton",
    "ImportFileButton",
    "CloseWorkspaceButton",
    "SettingsButton",
    "LoadDemoRunnerButton",
    "ImportRawButton",
]
button_positions: dict[str, int] = {}
button_visible: dict[str, bool] = {}
for name in button_names:
    token = f'x:Name="{name}"'
    index = toolstrip_text.find(token)
    button_positions[name] = index
    if index < 0:
        button_visible[name] = False
        reasons.append(f"Avalonia toolstrip is missing required button '{name}'.")
        continue
    button_end = toolstrip_text.find("/>", index)
    snippet = toolstrip_text[index:button_end if button_end > index else index + 160]
    button_visible[name] = 'IsVisible="False"' not in snippet

ordered_visible_buttons = []
for name, position in sorted(button_positions.items(), key=lambda item: item[1]):
    if position >= 0 and button_visible[name]:
        ordered_visible_buttons.append(name)
if ordered_visible_buttons[:3] != ["SaveButton", "PrintButton", "CopyButton"]:
    reasons.append(
        "Avalonia toolbar no longer starts with the classic save/print/copy rhythm: "
        + ", ".join(ordered_visible_buttons[:6] or ["<none>"])
    )
if button_visible.get("ImportRawButton", False):
    reasons.append("Raw XML import is still visible on the primary Avalonia toolbar.")

required_toolstrip_codebehind_tokens = [
    'SetButtonLabel(PrintButton, "Print Character", "Print");',
    'SetButtonLabel(CopyButton, "Copy", "Copy");',
    "public event EventHandler? PrintRequested;",
    "public event EventHandler? CopyRequested;",
]
for token in required_toolstrip_codebehind_tokens:
    if token not in toolstrip_codebehind_text:
        reasons.append(f"Avalonia toolstrip code-behind is missing required parity token: {token}")

if 'ColumnDefinitions="0,*,0"' not in main_window_text:
    reasons.append("Avalonia main window must default to a center-first 0,*,0 content layout for the single-runner shell.")
if 'x:Name="LeftNavigatorRegion"' not in main_window_text or 'IsVisible="False"' not in main_window_text:
    reasons.append("Avalonia main window must keep the workspace rail hidden in the default XAML posture.")
for token in [
    "ApplyWorkbenchChromeVisibility(shellFrame);",
    "new GridLength(228)",
    "new GridLength(0)",
]:
    if token not in main_window_state_refresh_text:
        reasons.append(f"Avalonia shell refresh is missing required conditional workspace-rail token: {token}")
if "ShowNavigatorPane: resolvedOpenWorkspaces.Length > 1" not in avalonia_projector_text:
    reasons.append("Avalonia shell projector must only surface the navigator pane for multi-workspace sessions.")
right_shell_index = main_window_text.find('x:Name="RightShellRegion"')
right_shell_snippet = (
    main_window_text[right_shell_index:right_shell_index + 420]
    if right_shell_index >= 0
    else ""
)
required_collapsed_right_shell_tokens = [
    'Width="0"',
    'MinWidth="0"',
    'MaxWidth="0"',
    'Opacity="0"',
    'IsHitTestVisible="False"',
    'ClipToBounds="True"',
]
if right_shell_index < 0 or any(token not in right_shell_snippet for token in required_collapsed_right_shell_tokens):
    reasons.append("Avalonia main window still exposes the right-side rail by default.")
if "DesktopHomeWindow.ShowIfNeededAsync(" in app_text:
    reasons.append("Avalonia startup still reopens the desktop home cockpit.")

required_event_tokens = [
    'ExecuteCommandAsync("print_character", CancellationToken.None)',
    'ExecuteCommandAsync("copy", CancellationToken.None)',
]
for token in required_event_tokens:
    if token not in main_window_event_handlers_text:
        reasons.append(f"Avalonia main window event routing is missing required parity command: {token}")

required_binding_tokens = [
    "toolStrip.PrintRequested += onPrintRequested;",
    "toolStrip.CopyRequested += onCopyRequested;",
]
for token in required_binding_tokens:
    if token not in main_window_control_binding_text:
        reasons.append(f"Avalonia control binding is missing required parity hook: {token}")

required_section_tokens = [
    'x:Name="ClassicCharacterSheetBorder"',
    'x:Name="ClassicAttributeFactsPanel"',
]
for token in required_section_tokens:
    if token not in section_host_text:
        reasons.append(f"Section host is missing required classic density marker: {token}")

section_payload_index = section_host_text.find('Header="Section Payload"')
section_payload_snippet = (
    section_host_text[section_payload_index:section_payload_index + 320]
    if section_payload_index >= 0
    else ""
)
required_collapsed_section_payload_tokens = [
    'Height="0"',
    'MinHeight="0"',
    'MaxHeight="0"',
    'Opacity="0"',
    'IsHitTestVisible="False"',
    'ClipToBounds="True"',
]
if section_payload_index < 0 or any(token not in section_payload_snippet for token in required_collapsed_section_payload_tokens):
    reasons.append('Section host is missing the zero-height "Section Payload" expander contract.')

raw_xml_index = section_host_text.find('Header="Raw XML Import"')
if raw_xml_index < 0 or 'IsExpanded="False"' not in section_host_text[raw_xml_index:raw_xml_index + 160]:
    reasons.append('Section host is missing the collapsed "Raw XML Import" expander contract.')

preferred_order_positions = {
    command: blazor_shell_text.find(f'"{command}"')
    for command in ["save_character", "print_character", "copy", "new_character", "open_character"]
}
if any(position < 0 for position in preferred_order_positions.values()):
    reasons.append("Blazor desktop shell preferred toolstrip order is missing one or more required classic commands.")
elif not (
    preferred_order_positions["save_character"]
    < preferred_order_positions["print_character"]
    < preferred_order_positions["copy"]
    < preferred_order_positions["new_character"]
    < preferred_order_positions["open_character"]
):
    reasons.append("Blazor desktop shell toolstrip order diverges from the classic save/print/copy-first contract.")

for token in [
    "@if (ShowLeftPane)",
    "workspace-layout--with-left-pane",
    "workspace-layout--without-left-pane",
]:
    if token not in blazor_shell_markup_text:
        reasons.append(f"Blazor desktop shell markup is missing required compact-layout token: {token}")
if "_shellSurfaceState.OpenWorkspaces.Count > 1" not in blazor_shell_text:
    reasons.append("Blazor desktop shell must only show the workspace rail for multi-workspace sessions.")
if "grid-template-columns: 228px minmax(0, 1fr);" not in blazor_css_text or "grid-template-columns: minmax(0, 1fr);" not in blazor_css_text:
    reasons.append("Blazor desktop shell CSS must support both compact single-runner and multi-workspace layouts.")
if ".right-pane {\n    display: none;" not in blazor_css_text and ".right-pane {\r\n    display: none;" not in blazor_css_text:
    reasons.append("Blazor desktop shell still exposes the right-side rail.")

resolver_tokens = [
    '["file", "edit", "special", "tools", "windows", "help"]',
    '["save_character", "print_character", "copy"]',
    'Command("special", "command.special", "menu", false)',
    'Command("copy", "command.copy", "edit", true)',
]
for token in resolver_tokens:
    if token not in resolver_text:
        reasons.append(f"Shared shell catalog is missing required classic parity token: {token}")

project_tokens = [
    ("Avalonia", avalonia_project_text),
    ("Blazor Desktop", blazor_desktop_project_text),
]
for label, text in project_tokens:
    if "Samples/Legacy/Soma-Career.chum5" not in text or "<CopyToPublishDirectory>Always</CopyToPublishDirectory>" not in text:
        reasons.append(f"{label} project is missing the published legacy demo runner sample.")

for script_label, script_text in [("release", release_gate_text), ("visual", visual_gate_text)]:
    if "chummer5a-layout-hard-gate.sh" not in script_text:
        reasons.append(f"The {script_label} gate is not wired to the Chummer5a layout hard gate.")

required_test_name = "Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers"
if required_test_name not in ui_gate_tests_text:
    reasons.append("Avalonia flagship UI gate tests do not include the Chummer5a layout hard gate wiring proof.")
for test_name in [
    "DesktopShell_hides_workspace_left_pane_for_single_runner_posture",
    "DesktopShell_restores_workspace_left_pane_for_multi_workspace_session",
]:
    if test_name not in desktop_shell_ruleset_tests_text:
        reasons.append(f"Desktop shell ruleset tests are missing required layout proof: {test_name}")

evidence.update(
    {
        "legacyFrmCareerDesignerPath": str(legacy_frmcareer_designer_path),
        "legacyMenuMarkers": legacy_menu_markers,
        "legacyToolbarMarkers": legacy_toolbar_markers,
        "legacyTabCount": legacy_tab_count,
        "menuHeaders": menu_headers,
        "expectedMenuHeaders": expected_menu_headers,
        "blazorRoot": str(blazor_shell_path.parent.parent.parent),
        "toolstripButtonPositions": button_positions,
        "toolstripButtonVisibility": button_visible,
        "orderedVisibleButtons": ordered_visible_buttons,
        "blazorPreferredToolstripOrderPositions": preferred_order_positions,
        "requiredTestName": required_test_name,
        "mainWindowDefaultContentColumns": "0,*,0" if 'ColumnDefinitions="0,*,0"' in main_window_text else "missing",
        "blazorSupportsCompactSingleRunnerLayout": "grid-template-columns: minmax(0, 1fr);" in blazor_css_text,
        "defaultSingleRunnerKeepsWorkspaceChromeCollapsed": (
            'ColumnDefinitions="0,*,0"' in main_window_text
            and 'IsVisible="False"' in main_window_text
            and "_shellSurfaceState.OpenWorkspaces.Count > 1" in blazor_shell_text
        ),
    }
)

payload = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.chummer5a_layout_hard_gate",
    "status": "pass" if not reasons else "fail",
    "summary": (
        "Chummer5a shell-layout parity is wired as a hard release gate."
        if not reasons
        else "Chummer5a shell-layout parity still diverges from the hard-gated desktop contract."
    ),
    "reasons": reasons,
    "evidence": evidence,
}

write_receipt(receipt_path, payload)
if reasons:
    raise SystemExit(61)
PY

echo "[chummer5a-layout-hard-gate] PASS"
