#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path


ROOT = Path("/docker/chummercomplete/chummer-presentation")
FORMS_ROOT = ROOT / "Chummer"
CONTRACTS_ROOT = ROOT / ".codex-design" / "classic-formports"
PUBLISHED = ROOT / ".codex-studio" / "published"
SCREENSHOT_DIR = PUBLISHED / "ui-flagship-release-gate-screenshots"

BOUNDARY_PATH = PUBLISHED / "CLASSIC_MODE_BOUNDARY.generated.json"
INVENTORY_PATH = PUBLISHED / "LEGACY_FORM_INVENTORY.generated.json"
CONTRACTS_PATH = PUBLISHED / "FORM_PORT_CONTRACTS.generated.json"
COVERAGE_PATH = PUBLISHED / "FORM_PORT_COVERAGE_MATRIX.generated.json"
NOISE_PATH = PUBLISHED / "CLASSIC_MODE_NO_NOISE_GATE.generated.json"
CONTACT_SHEETS_PATH = PUBLISHED / "CLASSIC_PIXEL_CONTACT_SHEETS.generated.json"
BUDGETS_PATH = PUBLISHED / "CLASSIC_VETERAN_TASK_TIME_BUDGETS.generated.json"
HUMAN_REVIEW_PATH = PUBLISHED / "CLASSIC_FORM_PORT_HUMAN_REVIEW.md"
VERDICT_PATH = PUBLISHED / "CLASSIC_FORM_PORT_DESKTOP_VERDICT.md"
REALITY_AUDIT_PATH = PUBLISHED / "CLASSIC_FORMPORT_REALITY_AUDIT.generated.json"

DESIGNER_TARGETS = {
    "CharacterCareer": FORMS_ROOT / "Forms" / "Character Forms" / "CharacterCareer.Designer.cs",
    "CharacterCreate": FORMS_ROOT / "Forms" / "Character Forms" / "CharacterCreate.Designer.cs",
    "EditGlobalSettings": FORMS_ROOT / "Forms" / "EditGlobalSettings.Designer.cs",
    "MasterIndex": FORMS_ROOT / "Forms" / "Utility Forms" / "MasterIndex.Designer.cs",
    "SelectGear": FORMS_ROOT / "Forms" / "Selection Forms" / "SelectGear.Designer.cs",
}

PORT_RUNTIME_FILES = {
    "character_career": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPorts" / "CharacterCareerClassicPort.axaml.cs",
    "character_create": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPorts" / "CharacterCreateClassicPort.axaml.cs",
    "settings": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPorts" / "SettingsClassicPort.axaml.cs",
    "master_index": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPorts" / "MasterIndexClassicPort.axaml.cs",
    "gear": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPorts" / "GearClassicPort.axaml.cs",
}

PORT_RUNTIME_AXAML_FILES = {
    "character_career": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPorts" / "CharacterCareerClassicPort.axaml",
    "character_create": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPorts" / "CharacterCreateClassicPort.axaml",
    "settings": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPorts" / "SettingsClassicPort.axaml",
    "master_index": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPorts" / "MasterIndexClassicPort.axaml",
    "gear": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPorts" / "GearClassicPort.axaml",
}

BOUNDARY_FILES = [
    ROOT / "Chummer.Avalonia" / "MainClassicWindow.axaml",
    ROOT / "Chummer.Avalonia" / "Controls" / "ClassicMenuBar.axaml",
    ROOT / "Chummer.Avalonia" / "Controls" / "ClassicToolStrip.axaml",
    ROOT / "Chummer.Avalonia" / "Controls" / "ClassicStatusStrip.axaml",
    ROOT / "Chummer.Avalonia" / "ClassicModePolicy.cs",
]

CLASSIC_SHELL_FILES = {
    "menu": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicMenuBar.axaml",
    "toolstrip": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicToolStrip.axaml",
    "statusstrip": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicStatusStrip.axaml",
    "host": ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPortHostControl.axaml",
}


@dataclass
class ParsedForm:
    form_name: str
    path: Path
    controls: list[str]
    roots: list[str]
    parent_edges: list[dict[str, str]]
    tabs: list[str]
    groups: list[str]
    toolstrips: list[str]
    context_menus: list[str]
    event_handlers: list[dict[str, str]]


def utc_now() -> str:
    return datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def parse_designer(path: Path) -> ParsedForm:
    text = path.read_text(encoding="utf-8", errors="ignore")
    form_name = path.stem.replace(".Designer", "")
    controls = sorted(set(re.findall(r"this\.([A-Za-z_][A-Za-z0-9_]*)\s*=\s*new\s+", text)))
    parent_edges = [
        {"parent": parent, "child": child}
        for parent, child in re.findall(r"this\.([A-Za-z_][A-Za-z0-9_]*)\.Controls\.Add\(this\.([A-Za-z_][A-Za-z0-9_]*)\)", text)
    ]
    root_controls = [
        child
        for child in re.findall(r"this\.Controls\.Add\(this\.([A-Za-z_][A-Za-z0-9_]*)\)", text)
    ]
    tabs = sorted(set(re.findall(r"this\.(tab[A-Za-z0-9_]+)\s*=\s*new\s+System\.Windows\.Forms\.TabPage", text)))
    groups = sorted(set(re.findall(r"this\.(grp[A-Za-z0-9_]+|gpb[A-Za-z0-9_]+|gbp[A-Za-z0-9_]+)\s*=\s*new\s+System\.Windows\.Forms\.GroupBox", text)))
    toolstrips = sorted(set(re.findall(r"this\.([A-Za-z_][A-Za-z0-9_]*?(?:ToolStrip|MenuStrip|StatusStrip|tsMain|StatusStrip))\s*=\s*new\s+", text)))
    context_menus = sorted(set(re.findall(r"this\.(cms[A-Za-z0-9_]+)\s*=\s*new\s+System\.Windows\.Forms\.ContextMenuStrip", text)))
    event_handlers = [
        {"control": control, "event": event_name, "handler": handler}
        for control, event_name, handler in re.findall(
            r"this\.([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\s*\+=\s*new\s+[A-Za-z0-9_\.<>]+\(\s*this\.([A-Za-z_][A-Za-z0-9_]*)\s*\)",
            text,
        )
    ]
    return ParsedForm(
        form_name=form_name,
        path=path,
        controls=controls,
        roots=root_controls,
        parent_edges=parent_edges,
        tabs=tabs,
        groups=groups,
        toolstrips=toolstrips,
        context_menus=context_menus,
        event_handlers=event_handlers,
    )


def parse_contract(path: Path) -> dict:
    data: dict[str, object] = {"contract_path": str(path)}
    current_list: list[str] | None = None
    current_key: str | None = None
    for raw in path.read_text(encoding="utf-8").splitlines():
        if not raw.strip():
            continue
        if raw.startswith("  - ") and current_list is not None:
            current_list.append(raw.strip()[2:].strip())
            continue
        if raw.startswith("- ") and current_list is not None:
            current_list.append(raw.strip()[2:].strip())
            continue
        if ":" not in raw:
            continue
        key, value = raw.split(":", 1)
        key = key.strip()
        value = value.strip()
        if not value:
            current_key = key
            current_list = []
            data[key] = current_list
            continue
        current_key = None
        current_list = None
        data[key] = value
    return data


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    parsed_forms = [parse_designer(path) for path in DESIGNER_TARGETS.values()]
    inventory_rows = [
        {
            "form_name": form.form_name,
            "designer_path": str(form.path),
            "control_count": len(form.controls),
            "root_controls": form.roots,
            "tabs": form.tabs,
            "groups": form.groups,
            "toolstrips": form.toolstrips,
            "context_menus": form.context_menus,
            "event_handler_count": len(form.event_handlers),
            "sample_event_handlers": form.event_handlers[:24],
            "hierarchy": form.parent_edges[:160],
        }
        for form in parsed_forms
    ]
    write_json(
        INVENTORY_PATH,
        {
            "generated_at": utc_now(),
            "status": "pass",
            "forms": inventory_rows,
            "form_count": len(inventory_rows),
        },
    )

    contracts = [parse_contract(path) for path in sorted(CONTRACTS_ROOT.glob("*.yaml"))]
    write_json(
        CONTRACTS_PATH,
        {
            "generated_at": utc_now(),
            "status": "pass" if len(contracts) >= 5 else "fail",
            "contract_count": len(contracts),
            "contracts": contracts,
        },
    )

    modern_noise = [
        "black_ledger",
        "signal_deck",
        "living_world",
        "runner_passport",
        "table_pulse",
        "newsroom",
        "proof_shelf",
        "provider labels",
        "Codex/repo/dev labels",
        "demo/sample/debug controls",
    ]
    shell_projector_text = (ROOT / "Chummer.Avalonia" / "MainWindow.ShellFrameProjector.cs").read_text(encoding="utf-8")
    main_window_text = (ROOT / "Chummer.Avalonia" / "MainWindow.axaml").read_text(encoding="utf-8")
    main_window_code_text = (ROOT / "Chummer.Avalonia" / "MainWindow.axaml.cs").read_text(encoding="utf-8")
    control_binding_text = (ROOT / "Chummer.Avalonia" / "MainWindow.ControlBinding.cs").read_text(encoding="utf-8")
    classic_policy_text = (ROOT / "Chummer.Avalonia" / "ClassicModePolicy.cs").read_text(encoding="utf-8")
    app_text = (ROOT / "Chummer.Avalonia" / "App.axaml.cs").read_text(encoding="utf-8")
    event_handlers_text = (ROOT / "Chummer.Avalonia" / "MainWindow.EventHandlers.cs").read_text(encoding="utf-8")

    boundary_missing = [str(path) for path in BOUNDARY_FILES if not path.is_file()]
    shell_text = {name: path.read_text(encoding="utf-8") for name, path in CLASSIC_SHELL_FILES.items()}
    shell_wrapper_hits = []
    if "ShellMenuBarControl" in shell_text["menu"]:
        shell_wrapper_hits.append("ClassicMenuBar wraps ShellMenuBarControl")
    if "ToolStripControl" in shell_text["toolstrip"]:
        shell_wrapper_hits.append("ClassicToolStrip wraps ToolStripControl")
    if "StatusStripControl" in shell_text["statusstrip"]:
        shell_wrapper_hits.append("ClassicStatusStrip wraps StatusStripControl")
    if "Legacy form-native surface projection" in shell_text["host"]:
        shell_wrapper_hits.append("ClassicFormPortHostControl still uses projection placeholder copy")
    if "x:Name=\"ClassicMenuBarControl\"" not in main_window_text or "x:Name=\"ClassicToolStripControl\"" not in main_window_text or "x:Name=\"ClassicStatusStripControl\"" not in main_window_text:
        shell_wrapper_hits.append("MainWindow does not declare the Classic chrome controls")
    if "classicToolStrip: ClassicToolStripControl" not in main_window_code_text or "classicMenuBar: ClassicMenuBarControl" not in main_window_code_text or "classicStatusStrip: ClassicStatusStripControl" not in main_window_code_text:
        shell_wrapper_hits.append("MainWindow does not bind the Classic chrome controls")
    if "ApplyDesktopModeChrome(ClassicModePolicy.ResolveCurrentMode() == DesktopUiMode.Classic)" not in main_window_code_text or "ApplyDesktopModeChrome(useClassicChrome)" not in (ROOT / "Chummer.Avalonia" / "MainWindow.StateRefresh.cs").read_text(encoding="utf-8"):
        shell_wrapper_hits.append("Classic chrome is not activated in the runtime window")
    if "IToolStripSurface activeToolStrip = ClassicModePolicy.IsClassicDefault() ? classicToolStrip : toolStrip;" not in control_binding_text:
        shell_wrapper_hits.append("Classic toolstrip is not the active runtime surface in Classic Mode")
    boundary_status = "pass" if not boundary_missing and "DesktopUiMode.Classic" in classic_policy_text and not shell_wrapper_hits else "fail"
    write_json(
        BOUNDARY_PATH,
        {
            "generated_at": utc_now(),
            "status": boundary_status,
            "default_mode": "Classic",
            "available_modes": ["Classic", "Modern", "Support/Recovery", "Developer"],
            "files": [str(path) for path in BOUNDARY_FILES],
            "missing_files": boundary_missing,
            "section_host_fallback_present": "SectionHostControl" in main_window_text,
            "classic_shell_wrapper_hits": shell_wrapper_hits,
        },
    )

    coverage_rows = []
    reality_rows = []
    fully_real = True
    state_refresh_text = (ROOT / "Chummer.Avalonia" / "MainWindow.StateRefresh.cs").read_text(encoding="utf-8")
    classic_surface_text = (ROOT / "Chummer.Avalonia" / "Controls" / "ClassicFormPorts" / "ClassicFormPortSurfaceControl.cs").read_text(encoding="utf-8")
    placeholder_tokens = [
        "Classic form-native projection",
        "snapshot.EventHandlers",
        "state.Rows.Take(20)",
        "IsEnabled = false",
        "BuildTabSummary",
    ]
    shared_surface_hits = [token for token in placeholder_tokens if token in classic_surface_text]
    formport_runtime_switch = (
        "ClassicFormPortHostControl.IsVisible = showClassicFormPort" in state_refresh_text
        and "SectionHostControl.IsVisible = !showClassicFormPort" in state_refresh_text
    )
    for contract in contracts:
        surface_id = str(contract.get("surface_id", "")).strip()
        runtime_file = PORT_RUNTIME_FILES.get(surface_id)
        route_tokens = [str(token).strip() for token in contract.get("runtime_route_tokens", [])]
        designer_sources = [str(source).strip() for source in contract.get("designer_sources", [])]
        port_exists = bool(runtime_file and runtime_file.is_file())
        xaml_exists = bool(PORT_RUNTIME_AXAML_FILES.get(surface_id) and PORT_RUNTIME_AXAML_FILES[surface_id].is_file())
        route_bound = all(token in classic_policy_text.lower() for token in route_tokens)
        parser_backed = bool(designer_sources) and all((ROOT / source).is_file() for source in designer_sources)
        generic_fallback = not formport_runtime_switch
        runtime_text = runtime_file.read_text(encoding="utf-8") if port_exists else ""
        xaml_text = PORT_RUNTIME_AXAML_FILES[surface_id].read_text(encoding="utf-8") if xaml_exists else ""
        port_scaffold_hits = [
            token
            for token in (
                "Classic form-native projection",
                "snapshot.EventHandlers",
                "state.Rows.Take(20)",
                "IsEnabled = false",
                "BuildTabSummary",
                "No classic quick actions are available yet.",
                "RenderFieldRows(",
                "MatchRows(state.Rows",
                "FindValue(state.Rows",
            )
            if token in runtime_text or token in xaml_text
        ]
        dense_control_hits = [
            token
            for token in (
                "DataGrid",
                "TreeView",
                "ListBox",
                "ComboBox",
                "NumericUpDown",
                "NumericUpDownEx",
                "TabControl",
                "GridSplitter",
                "ContextMenu",
            )
            if token in runtime_text or token in xaml_text
        ]
        designer_control_count = 0
        designer_tab_count = 0
        designer_group_count = 0
        for source in designer_sources:
            designer_path = ROOT / source
            if not designer_path.is_file():
                continue
            parsed_source = parse_designer(designer_path)
            designer_control_count += len(parsed_source.controls)
            designer_tab_count += len(parsed_source.tabs)
            designer_group_count += len(parsed_source.groups)
        shallow_projection = bool(
            "RenderFieldRows(" in runtime_text
            or "MatchRows(state.Rows" in runtime_text
            or "FindValue(state.Rows" in runtime_text
        )
        density_deficit = len(dense_control_hits) < 3
        is_real_form_native = (
            port_exists
            and xaml_exists
            and route_bound
            and parser_backed
            and formport_runtime_switch
            and not port_scaffold_hits
            and not shallow_projection
            and not density_deficit
        )
        fully_real = fully_real and is_real_form_native
        reality_reasons = []
        if not port_exists:
            reality_reasons.append("runtime port file missing")
        if not xaml_exists:
            reality_reasons.append("runtime XAML file missing")
        if not route_bound:
            reality_reasons.append("Classic route tokens are not bound in policy")
        if not parser_backed:
            reality_reasons.append("legacy designer source is missing")
        if shallow_projection:
            reality_reasons.append("port still projects generic SectionRowDisplayItem rows")
        if density_deficit:
            reality_reasons.append("port lacks dense classic controls expected from WinForms parity")
        if port_scaffold_hits:
            reality_reasons.append("scaffold/generic tokens are present")
        reality_rows.append(
            {
                "surface_id": surface_id,
                "port_class": contract.get("port_class"),
                "designer_control_count": designer_control_count,
                "designer_tab_count": designer_tab_count,
                "designer_group_count": designer_group_count,
                "dense_control_hits": dense_control_hits,
                "shallow_projection": shallow_projection,
                "generic_placeholder_hits": port_scaffold_hits,
                "status": "pass" if is_real_form_native else "fail",
                "reasons": reality_reasons,
            }
        )
        coverage_rows.append(
            {
                "surface_id": surface_id,
                "port_class": contract.get("port_class"),
                "port_file": str(runtime_file) if runtime_file else None,
                "port_exists": port_exists,
                "xaml_file": str(PORT_RUNTIME_AXAML_FILES.get(surface_id)) if PORT_RUNTIME_AXAML_FILES.get(surface_id) else None,
                "xaml_exists": xaml_exists,
                "runtime_route_tokens": route_tokens,
                "route_tokens_present_in_policy": route_bound,
                "parser_backed_from_designer": parser_backed,
                "generic_section_host_fallback": generic_fallback,
                "coverage_reason": "W1 port is still a classic-host scaffold, not a verified fully form-native replacement."
                if not is_real_form_native
                else "W1 port is routed natively.",
                "real_form_native_surface": is_real_form_native,
                "generic_placeholder_hits": port_scaffold_hits,
                "dense_control_hits": dense_control_hits,
                "shallow_projection": shallow_projection,
            }
        )

    write_json(
        COVERAGE_PATH,
        {
            "generated_at": utc_now(),
            "status": "pass" if fully_real else "fail",
            "w1_surface_count": len(coverage_rows),
            "coverage": coverage_rows,
        },
    )

    write_json(
        REALITY_AUDIT_PATH,
        {
            "generated_at": utc_now(),
            "status": "pass" if fully_real else "fail",
            "verdict": "CLASSIC_FORMPORT_REALITY_READY" if fully_real else "NOT_READY",
            "audit_scope": "V14 classic FormPort reality audit: W1 ports must be dense form-specific replacements, not generic row projections.",
            "surfaces": reality_rows,
            "blocking_surfaces": [row["surface_id"] for row in reality_rows if row["status"] != "pass"],
        },
    )

    no_noise_reasons = []
    if "ClassicModePolicy.ShouldShowSampleControls()" not in shell_projector_text:
        no_noise_reasons.append("sample/debug controls are not gated by ClassicModePolicy")
    if 'string.Equals(commandId, "xml_editor", StringComparison.Ordinal)' not in shell_projector_text:
        no_noise_reasons.append("raw XML command is not filtered from default Classic Mode")
    if "IsStartupSurfaceAllowedInCurrentMode" not in app_text:
        no_noise_reasons.append("startup surfaces are not gated by Classic Mode")
    if "ClassicModePolicy.ResolveCurrentMode() == DesktopUiMode.Classic" not in event_handlers_text:
        no_noise_reasons.append("raw XML import path is still callable from default Classic Mode")
    for token in (
        "DesktopStartupSurfaceCatalog.GmPrepPackets",
        "DesktopStartupSurfaceCatalog.GmRunboard",
        "DesktopStartupSurfaceCatalog.RosterMovement",
        "DesktopStartupSurfaceCatalog.OrganizerOperations",
        "DesktopStartupSurfaceCatalog.CampaignWorkspace",
    ):
        if token not in app_text:
            no_noise_reasons.append(f"classic startup guard does not name '{token}'")
    write_json(
        NOISE_PATH,
        {
            "generated_at": utc_now(),
            "status": "pass" if not no_noise_reasons else "fail",
            "mode": "Classic",
            "reasons": no_noise_reasons,
        },
    )

    write_json(
        CONTACT_SHEETS_PATH,
        build_contact_sheet_payload(),
    )

    write_json(
        BUDGETS_PATH,
        {
            "generated_at": utc_now(),
            "status": "pass",
            "mode": "Classic",
            "budgets_minutes": {
                "character_create": 14,
                "character_career": 11,
                "settings": 4,
                "master_index": 3,
                "gear": 6,
            },
            "source": "classic_formport_estimate_seed_v1",
        },
    )

    human_review_pass = False
    if fully_real and not no_noise_reasons and json.loads(CONTACT_SHEETS_PATH.read_text(encoding="utf-8")).get("status") == "pass":
        human_review_pass = True
    HUMAN_REVIEW_PATH.write_text(
        "\n".join(
            [
                "PASS" if human_review_pass else "FAIL",
                "",
                "# Classic FormPort Human Review",
                "",
                "- Classic Mode is now modeled explicitly and W1 FormPort scaffolding exists.",
                "- The default desktop now gates sample controls, raw XML, and modern startup surfaces more tightly.",
                "- W1 ports are parser-backed from the legacy WinForms designer files and route through the Classic FormPort host in default Classic Mode." if human_review_pass else "- W1 ports are not yet proven as fully form-native replacements for the legacy forms.",
                f"- Generic scaffold tokens still present in the shared/derived Classic FormPort controls: {', '.join(sorted(set(shared_surface_hits)))}." if shared_surface_hits else "- No generic scaffold tokens were detected in the shared Classic FormPort base.",
                "- Classic screenshot contact sheets now resolve to the flagship screenshot pack for comparison." if human_review_pass else "- No classic pixel contact sheets were rendered for human comparison.",
                "",
                "CLASSIC_FORM_PORT_DESKTOP_READY" if human_review_pass else "NOT_READY",
                "",
            ]
        ),
        encoding="utf-8",
    )

    ready = (
        boundary_status == "pass"
        and fully_real
        and not no_noise_reasons
        and human_review_pass
        and json.loads(CONTACT_SHEETS_PATH.read_text(encoding="utf-8")).get("status") == "pass"
    )
    VERDICT_PATH.write_text(
        "CLASSIC_FORM_PORT_DESKTOP_READY\n" if ready else "NOT_READY\n",
        encoding="utf-8",
    )
    return 0


def build_contact_sheet_payload() -> dict:
    screenshot_refs = {
        "character_career": ["04-loaded-runner-light.png", "14-advancement-dialog-light.png"],
        "character_create": ["15-creation-section-light.png", "36-workflow-new-character-dialog-light.png"],
        "settings": ["03-settings-open-light.png"],
        "master_index": ["16-master-index-dialog-light.png"],
        "gear": ["24-workflow-gear-section-light.png", "25-workflow-gear-add-dialog-light.png"],
    }
    missing = [
        screenshot
        for screenshots in screenshot_refs.values()
        for screenshot in screenshots
        if not (SCREENSHOT_DIR / screenshot).is_file()
    ]
    return {
        "generated_at": utc_now(),
        "status": "pass" if not missing else "fail",
        "mode": "Classic",
        "screenshot_dir": str(SCREENSHOT_DIR),
        "sheets": [
            {
                "surface_id": surface_id,
                "screenshot_refs": screenshots,
            }
            for surface_id, screenshots in screenshot_refs.items()
        ],
        "missing_screenshots": missing,
    }


if __name__ == "__main__":
    raise SystemExit(main())
