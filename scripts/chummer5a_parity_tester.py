#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import yaml


REPO_ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = REPO_ROOT.parent


def default_chummer5a_repo_path() -> Path:
    path_override = os.environ.get("CHUMMER5A_REPO_PATH")
    if path_override:
        return Path(path_override)

    legacy_override = os.environ.get("CHUMMER5A_REPO_URL")
    if legacy_override and "://" not in legacy_override:
        return Path(legacy_override)

    return WORKSPACE_ROOT / "chummer5a"


def default_chummer5a_repo_source() -> str:
    if os.environ.get("CHUMMER5A_REPO_PATH"):
        return "CHUMMER5A_REPO_PATH"

    legacy_override = os.environ.get("CHUMMER5A_REPO_URL")
    if legacy_override and "://" not in legacy_override:
        return "CHUMMER5A_REPO_URL_legacy_path"

    if legacy_override:
        return "sibling_default_ignored_url_shaped_CHUMMER5A_REPO_URL"

    return "sibling_default"


def chummer5a_repo_arg_provided() -> bool:
    return any(arg == "--chummer5a-repo" or arg.startswith("--chummer5a-repo=") for arg in sys.argv[1:])


DEFAULT_CHUMMER5A_REPO = default_chummer5a_repo_path()
DEFAULT_CHUMMER5A_REPO_SOURCE = default_chummer5a_repo_source()
DEFAULT_CHUMMER5A_REF = os.environ.get("CHUMMER5A_REF", "HEAD")
DEFAULT_PARITY_LAB_ROOT = Path(
    os.environ.get(
        "CHUMMER5A_PARITY_LAB_ROOT",
        WORKSPACE_ROOT / "EA" / "docs" / "chummer5a_parity_lab",
    )
)
DEFAULT_FIXTURE_INVENTORY = DEFAULT_PARITY_LAB_ROOT / "import_export_fixture_inventory.yaml"
DEFAULT_ORACLE_BASELINES = DEFAULT_PARITY_LAB_ROOT / "oracle_baselines.yaml"
DEFAULT_WORKFLOW_PACK = DEFAULT_PARITY_LAB_ROOT / "veteran_workflow_pack.yaml"
DEFAULT_SCREENSHOT_EVIDENCE = REPO_ROOT / ".codex-studio" / "published" / "ui-flagship-release-gate-screenshots" / "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
DEFAULT_VISUAL_GATE = REPO_ROOT / ".codex-studio" / "published" / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
DEFAULT_FLAGSHIP_GATE = REPO_ROOT / ".codex-studio" / "published" / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
DEFAULT_EXECUTABLE_GATE = REPO_ROOT / ".codex-studio" / "published" / "DESKTOP_EXECUTABLE_EXIT_GATE.generated.json"
DEFAULT_WORKFLOW_PARITY_DIR = REPO_ROOT / ".codex-studio" / "published" / "workflow-family-parity" / "sr6"
DEFAULT_EXECUTED_WORKFLOW_PARITY_DIR = REPO_ROOT / ".codex-studio" / "published" / "workflow-family-parity" / "executed" / "sr6"
DEFAULT_USER_JOURNEY_TRACE = REPO_ROOT / ".codex-studio" / "published" / "USER_JOURNEY_TESTER_TRACE.generated.json"
DEFAULT_USER_JOURNEY_AUDIT = REPO_ROOT / ".codex-studio" / "published" / "USER_JOURNEY_TESTER_AUDIT.generated.json"
DEFAULT_USER_JOURNEY_SCREENSHOT_DIR = REPO_ROOT / ".codex-studio" / "published" / "user-journey-tester-screenshots"
DEFAULT_RECONSTRUCTION_RECEIPTS_DIR = REPO_ROOT / ".codex-studio" / "published" / "chummer5a-fixture-ui-reconstruction"
DEFAULT_DESKTOP_WORKFLOW_PARITY_RECEIPT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
DEFAULT_GENERATED_DIALOG_ELEMENT_PARITY_RECEIPT = REPO_ROOT / ".codex-studio" / "published" / "GENERATED_DIALOG_ELEMENT_PARITY.generated.json"
DEFAULT_UI_ELEMENT_PARITY_AUDIT_RECEIPT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"
DEFAULT_ARTIFACTS = REPO_ROOT / ".codex-studio" / "out" / "chummer5a-parity-tester"

PASS_EXIT = 0
FAIL_EXIT = 1
INFRA_EXIT = 2

REFERENCE_SENTINEL = "reference-screenshots-not-captured.txt"
DIFF_SENTINEL = "diff-images-not-generated.txt"
USER_JOURNEY_TRACE_CONTRACT = "chummer6-ui.user_journey_tester_trace"
USER_JOURNEY_AUDIT_CONTRACT = "chummer6-ui.user_journey_tester_audit"
WORKFLOW_VERIFICATION_CONTRACT = "chummer6-ui.sr6_workflow_family_verification_receipt"
EXECUTED_WORKFLOW_CONTRACT = "chummer6-ui.sr6_workflow_family_execution_receipt"
FIXTURE_RECONSTRUCTION_CONTRACT = "chummer6-ui.chummer5a_fixture_ui_reconstruction"
CHUMMER5A_DESKTOP_WORKFLOW_PARITY_CONTRACT = "chummer6-ui.chummer5a_desktop_workflow_parity"
GENERATED_DIALOG_ELEMENT_PARITY_CONTRACT = "chummer6-ui.generated_dialog_element_parity"
USER_JOURNEY_REQUIRED_WORKFLOW_ASSERTIONS: dict[str, tuple[str, ...]] = {
    "master_index_search_focus_stability": (
        "focus_preserved_after_typing",
        "search_text_accumulates_keyboard_input",
    ),
    "file_new_character_visible_workspace": (
        "new_character_action_opened_visible_workspace",
        "visible_workspace_nonblank",
        "starter_attributes_match_seeded_workspace",
        "section_preview_omits_review_copy",
    ),
    "minimal_character_build_save_reload": (
        "character_created_saved_reloaded",
        "reload_preserved_character_identity",
    ),
    "major_navigation_sanity": (
        "primary_navigation_clicks_change_visible_content",
        "no_unhandled_errors",
    ),
    "validation_or_export_smoke": (
        "validation_or_export_action_completed",
        "result_visible_or_file_created",
    ),
}

LANDMARK_ALIASES: dict[str, tuple[str, ...]] = {
    "File menu": ("File",),
    "Tools menu": ("Tools",),
    "Windows menu": ("Windows",),
    "Help menu": ("Help",),
    "Immediate toolstrip": ("New Character", "Section Payload"),
    "Save or open route": ("Open Character", "Save Character", "Save", "State:"),
    "Import route": ("Open Character", "Import", "LoadDemoRunnerButton"),
    "Settings route": ("Global Settings", "Modify..."),
    "Master index route": ("Master Index",),
    "Character roster route": ("Character Roster",),
    "Bottom status strip": ("Service: online", "Ruleset:", "Time:"),
}


@dataclass(frozen=True)
class FirstSliceCategory:
    category_id: str
    label: str
    preferred_filenames: tuple[str, ...]
    required_workflow_family_ids: tuple[str, ...]


FIRST_SLICE_CATEGORIES: tuple[FirstSliceCategory, ...] = (
    FirstSliceCategory(
        category_id="simple_mundane",
        label="Simple mundane character",
        preferred_filenames=("Bastion.chum5", "SCSi.chum5", "Wesson.chum5"),
        required_workflow_family_ids=(
            "create-open-import-save-save-as-print-export",
            "attributes-skills-skill-groups-specializations-knowledge-languages",
            "recovery-reload-migration-roundtrips",
        ),
    ),
    FirstSliceCategory(
        category_id="priority_generation",
        label="Priority or generation method choices",
        preferred_filenames=("Fuzzy-chargen.chum5", "Mittens Chargen.chum5", "Ushi Resub.chum5"),
        required_workflow_family_ids=("metatype-priorities-karma-entry",),
    ),
    FirstSliceCategory(
        category_id="gear_equipment",
        label="Meaningful gear and equipment",
        preferred_filenames=("Soma.chum5", "SCSi.chum5", "Bastion.chum5"),
        required_workflow_family_ids=("armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers",),
    ),
    FirstSliceCategory(
        category_id="modal_heavy",
        label="Modal-heavy workflow",
        preferred_filenames=("Soma.chum5", "Tenshi.chum5", "Munin.chum5"),
        required_workflow_family_ids=(
            "cyberware-bioware-modular-hierarchies-nested-plugins",
            "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms",
        ),
    ),
    FirstSliceCategory(
        category_id="validation_warning",
        label="Validation or warning behavior",
        preferred_filenames=("Popstar.chum5", "Fuzzy-chargen.chum5", "Ushi Resub.chum5"),
        required_workflow_family_ids=(
            "improvements-explain-result-parity",
            "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
        ),
    ),
)


@dataclass(frozen=True)
class FixtureSpec:
    category_id: str
    category_label: str
    file_path: Path
    fixture_name: str
    character_name: str
    feature_counts: dict[str, int]
    workflow_family_ids: tuple[str, ...]


@dataclass(frozen=True)
class Failure:
    fixture: str
    checkpoint: str
    step: int
    category: str
    severity: str
    expected: str
    actual: str
    reference_screenshot: str
    actual_screenshot: str
    diff_screenshot: str
    remediation_target: str


@dataclass(frozen=True)
class GateArtifacts:
    root: Path
    run_metadata: Path
    coverage_matrix: Path
    failures_md: Path
    failures_json: Path
    screenshots_reference: Path
    screenshots_actual: Path
    screenshots_diff: Path
    ui_actions_trace: Path
    reconstruction_log: Path


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def normalize_text(value: str) -> str:
    return " ".join(value.strip().lower().split())


def ensure_dir(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def write_json(path: Path, payload: Any) -> None:
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def write_lines(path: Path, lines: list[str]) -> None:
    path.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8")


def load_yaml(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        payload = yaml.safe_load(handle)
    if not isinstance(payload, dict):
        raise ValueError(f"YAML root must be a mapping: {path}")
    return payload


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise ValueError(f"JSON root must be an object: {path}")
    return payload


def normalize_contract_name(payload: dict[str, Any]) -> str:
    return str(payload.get("contract_name") or payload.get("contractName") or "").strip()


def status_pass(value: Any) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def git_rev_parse(repo: Path, ref: str) -> str:
    completed = subprocess.run(
        ["git", "-C", str(repo), "rev-parse", f"{ref}^{{commit}}"],
        check=False,
        capture_output=True,
        text=True,
    )
    if completed.returncode != 0:
        stderr = completed.stderr.strip() or completed.stdout.strip() or f"unable to resolve git ref {ref}"
        raise RuntimeError(stderr)
    return completed.stdout.strip()


def init_artifacts(root: Path) -> GateArtifacts:
    ensure_dir(root)
    screenshots_root = root / "screenshots"
    reference_dir = screenshots_root / "reference"
    actual_dir = screenshots_root / "actual"
    diff_dir = screenshots_root / "diff"
    traces_dir = root / "traces"
    for path in (reference_dir, actual_dir, diff_dir, traces_dir):
        ensure_dir(path)
    return GateArtifacts(
        root=root,
        run_metadata=root / "run-metadata.json",
        coverage_matrix=root / "coverage-matrix.md",
        failures_md=root / "failures.md",
        failures_json=root / "failures.json",
        screenshots_reference=reference_dir,
        screenshots_actual=actual_dir,
        screenshots_diff=diff_dir,
        ui_actions_trace=traces_dir / "ui-actions.jsonl",
        reconstruction_log=traces_dir / "reconstruction-log.txt",
    )


def parse_fixture(path: Path) -> FixtureSpec:
    root = ET.parse(path).getroot()
    feature_tags = (
        "attributes",
        "skills",
        "skillgroups",
        "knowledgeskills",
        "qualities",
        "contacts",
        "lifestyles",
        "notes",
        "expenses",
        "calendar",
        "gears",
        "armors",
        "weapons",
        "vehicles",
        "cyberwares",
        "biowares",
        "powers",
        "spells",
        "complexforms",
        "martialarts",
        "improvements",
        "spirits",
        "sprites",
        "metamagics",
        "echoes",
    )
    feature_counts: dict[str, int] = {}
    for tag in feature_tags:
        node = root.find(tag)
        if node is None:
            feature_counts[tag] = 0
        else:
            feature_counts[tag] = len(list(node)) if list(node) else (1 if (node.text or "").strip() else 0)

    priorities_present = any((root.findtext(tag) or "").strip() not in {"", "0"} for tag in (
        "prioritymetatype",
        "priorityattributes",
        "priorityspecial",
        "priorityskills",
        "priorityresources",
    ))
    workflow_family_ids = infer_workflow_families(feature_counts, priorities_present)
    character_name = (root.findtext("name") or path.stem).strip() or path.stem
    return FixtureSpec(
        category_id="",
        category_label="",
        file_path=path,
        fixture_name=path.name,
        character_name=character_name,
        feature_counts=feature_counts,
        workflow_family_ids=tuple(workflow_family_ids),
    )


def infer_workflow_families(feature_counts: dict[str, int], priorities_present: bool) -> list[str]:
    families = [
        "create-open-import-save-save-as-print-export",
        "recovery-reload-migration-roundtrips",
        "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
    ]
    if priorities_present:
        families.append("metatype-priorities-karma-entry")
    if feature_counts.get("attributes", 0) or feature_counts.get("skills", 0) or feature_counts.get("skillgroups", 0):
        families.append("attributes-skills-skill-groups-specializations-knowledge-languages")
    if any(feature_counts.get(tag, 0) for tag in ("qualities", "contacts", "lifestyles", "notes", "expenses", "calendar")):
        families.append("qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources")
    if any(feature_counts.get(tag, 0) for tag in ("gears", "armors", "weapons", "vehicles")):
        families.append("armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers")
    if any(feature_counts.get(tag, 0) for tag in ("cyberwares", "biowares")):
        families.append("cyberware-bioware-modular-hierarchies-nested-plugins")
    if any(feature_counts.get(tag, 0) for tag in ("powers", "spells", "complexforms", "martialarts", "spirits", "sprites", "metamagics", "echoes")):
        families.append("magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms")
    if feature_counts.get("improvements", 0):
        families.append("improvements-explain-result-parity")
    return sorted(dict.fromkeys(families))


def select_default_fixtures(chummer5a_repo: Path) -> list[FixtureSpec]:
    fixture_root = chummer5a_repo / "Chummer.Tests" / "TestFiles"
    available = sorted(fixture_root.glob("*.chum5"))
    if not available:
        raise FileNotFoundError(f"No .chum5 fixtures found under {fixture_root}")
    by_name = {path.name: path for path in available}
    selected: list[FixtureSpec] = []
    used_names: set[str] = set()
    for category in FIRST_SLICE_CATEGORIES:
        candidate_path = next((by_name[name] for name in category.preferred_filenames if name in by_name and name not in used_names), None)
        if candidate_path is None:
            candidate_path = next((path for path in available if path.name not in used_names), None)
        if candidate_path is None:
            raise RuntimeError(f"Unable to select fixture for category {category.category_id}")
        fixture = parse_fixture(candidate_path)
        workflow_family_ids = sorted(dict.fromkeys([*fixture.workflow_family_ids, *category.required_workflow_family_ids]))
        selected.append(
            FixtureSpec(
                category_id=category.category_id,
                category_label=category.label,
                file_path=fixture.file_path,
                fixture_name=fixture.fixture_name,
                character_name=fixture.character_name,
                feature_counts=fixture.feature_counts,
                workflow_family_ids=tuple(workflow_family_ids),
            )
        )
        used_names.add(candidate_path.name)
    return selected


def select_user_fixtures(raw_paths: list[str]) -> list[FixtureSpec]:
    selected: list[FixtureSpec] = []
    for raw_path in raw_paths:
        path = Path(raw_path).expanduser().resolve()
        fixture = parse_fixture(path)
        selected.append(
            FixtureSpec(
                category_id="user_selected",
                category_label="User selected fixture",
                file_path=fixture.file_path,
                fixture_name=fixture.fixture_name,
                character_name=fixture.character_name,
                feature_counts=fixture.feature_counts,
                workflow_family_ids=fixture.workflow_family_ids,
            )
        )
    return selected


def available_fixture_count(chummer5a_repo: Path) -> int:
    fixture_root = chummer5a_repo / "Chummer.Tests" / "TestFiles"
    return len(list(fixture_root.glob("*.chum5")))


def classify_fixture_scope(*, explicit_fixture_paths: list[str] | None, selected_fixture_count: int, available_fixture_total: int) -> str:
    if not explicit_fixture_paths:
        return "first_slice_default"
    if available_fixture_total > 0 and selected_fixture_count >= available_fixture_total:
        return "all_available_fixtures_explicit"
    return "explicit_fixture_set"


def pass_summary_for_scope(
    fixture_scope: str,
    *,
    selected_fixture_ui_certified: bool,
    recursive_settings_and_elements_certified: bool,
) -> str:
    if selected_fixture_ui_certified:
        if fixture_scope == "first_slice_default":
            return "Parity gate passed for the default first-slice Chummer5a fixture set."
        if fixture_scope == "all_available_fixtures_explicit":
            return "Parity gate passed for the explicitly selected all-fixtures Chummer5a set with per-fixture UI reconstruction proof."
        return "Parity gate passed for the explicitly selected Chummer5a fixture set with per-fixture UI reconstruction proof."
    if recursive_settings_and_elements_certified:
        if fixture_scope == "all_available_fixtures_explicit":
            return "Parity gate passed for the explicitly selected all-fixtures Chummer5a set with exhaustive recursive settings/element parity proof."
        if fixture_scope == "explicit_fixture_set":
            return "Parity gate passed for the explicitly selected Chummer5a fixture set with exhaustive recursive settings/element parity proof."
        return "Parity gate passed for the default first-slice Chummer5a fixture set with exhaustive recursive settings/element parity proof."
    return "Parity gate passed for the selected Chummer5a proof set."


def proof_claims(
    *,
    selected_fixture_ui_certified: bool,
    recursive_settings_and_elements_certified: bool,
) -> list[str]:
    claims = [
        "Selected Chummer5a fixtures were parsed as structural oracle inputs.",
        "Published Chummer6 workflow receipts, screenshot control evidence, and release gates covered the workflow families inferred from the selected fixtures.",
    ]
    if recursive_settings_and_elements_certified:
        claims.append(
            "Recursive menu workflows, legacy UI controls, quick-action roots, and generated dialog inventories are exhaustively classified and certified by published Chummer5a desktop workflow and element-parity receipts."
        )
    if selected_fixture_ui_certified:
        claims.append(
            "Each selected fixture had a passing per-fixture UI reconstruction receipt proving click-driven open, save-as, export, print-preview, PDF-route artifact materialization, reload, and identity-preserving roundtrip coverage."
        )
    return claims


def proof_limitations(
    *,
    selected_fixture_ui_certified: bool,
    full_fixture_ui_certified: bool,
    recursive_settings_and_elements_certified: bool,
) -> list[str]:
    limitations = [
        "This tester run validated published proof artifacts; it did not itself drive the Chummer6 UI end-to-end.",
        "Legacy Chummer5a reference screenshots were not captured in this tester run.",
        "Pixel diff images were not generated in this tester run.",
    ]
    if not selected_fixture_ui_certified:
        if recursive_settings_and_elements_certified:
            limitations.append("This pass is grounded in exhaustive recursive settings/element parity receipts rather than per-fixture UI reconstruction receipts.")
        limitations.append(
            "No per-fixture reconstruction, save-as, export, print-preview, PDF-route artifact, reload, or import/export roundtrip receipt was present for the selected fixture set."
        )
        limitations.append("A passing result does not certify that every selected .chum5 file can be rebuilt only through the Chummer6 UI.")
    elif not full_fixture_ui_certified:
        limitations.append("A passing result certifies only the explicitly selected fixture set, not the entire Chummer5a fixture corpus.")
    return limitations


def try_load_json(path: Path) -> tuple[dict[str, Any], list[str]]:
    if not path.is_file():
        return {}, [f"Missing JSON receipt: {path}"]
    try:
        return load_json(path), []
    except Exception as exc:  # noqa: BLE001
        return {}, [f"Unreadable JSON receipt {path}: {exc}"]


def normalize_path_string(path: Path) -> str:
    return str(path.expanduser().resolve())


def json_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def validate_user_journey_trace(trace_path: Path, screenshot_dir: Path) -> list[str]:
    payload, reasons = try_load_json(trace_path)
    if reasons:
        return reasons
    if normalize_contract_name(payload) != USER_JOURNEY_TRACE_CONTRACT:
        reasons.append(f"user journey trace contract_name must be {USER_JOURNEY_TRACE_CONTRACT}: {trace_path}")
    if not status_pass(payload.get("status")):
        reasons.append(f"user journey trace status is not pass/passed/ready: {trace_path}")
    if payload.get("linux_binary_under_test") is not True:
        reasons.append(f"user journey trace must prove the Linux desktop binary was exercised: {trace_path}")
    if payload.get("used_internal_apis") is not False:
        reasons.append(f"user journey trace must declare used_internal_apis=false: {trace_path}")
    tester_shard_id = str(payload.get("tester_shard_id") or "").strip()
    fix_shard_id = str(payload.get("fix_shard_id") or "").strip()
    if not tester_shard_id or not fix_shard_id or tester_shard_id == fix_shard_id:
        reasons.append(f"user journey trace must declare distinct tester_shard_id/fix_shard_id values: {trace_path}")
    blocking_findings = payload.get("open_blocking_findings") or []
    if isinstance(blocking_findings, list) and any(str(item or "").strip() for item in blocking_findings):
        reasons.append(f"user journey trace still reports open blocking findings: {trace_path}")

    workflows = payload.get("workflows") or []
    workflow_by_id = {
        str(item.get("id") or "").strip(): item
        for item in workflows
        if isinstance(item, dict) and str(item.get("id") or "").strip()
    }
    for workflow_id, required_assertions in USER_JOURNEY_REQUIRED_WORKFLOW_ASSERTIONS.items():
        row = workflow_by_id.get(workflow_id)
        if row is None:
            reasons.append(f"user journey trace is missing required workflow {workflow_id}: {trace_path}")
            continue
        if not status_pass(row.get("status")):
            reasons.append(f"user journey workflow {workflow_id} is not passing: {trace_path}")
        screenshots = row.get("screenshots") or []
        if not isinstance(screenshots, list) or len(screenshots) < 2:
            reasons.append(f"user journey workflow {workflow_id} must publish at least two screenshots: {trace_path}")
        else:
            for raw_screenshot in screenshots:
                screenshot_path = Path(str(raw_screenshot))
                if not screenshot_path.is_absolute():
                    screenshot_path = screenshot_dir / screenshot_path
                if not screenshot_path.is_file():
                    reasons.append(f"user journey workflow {workflow_id} is missing screenshot {screenshot_path}")
        assertions = row.get("assertions") or {}
        missing_assertions = [
            assertion
            for assertion in required_assertions
            if not isinstance(assertions, dict) or assertions.get(assertion) is not True
        ]
        if missing_assertions:
            reasons.append(
                f"user journey workflow {workflow_id} is missing required assertion(s): {', '.join(missing_assertions)}"
            )
    return reasons


def validate_user_journey_audit_receipt(audit_path: Path, trace_path: Path, screenshot_dir: Path) -> list[str]:
    payload, reasons = try_load_json(audit_path)
    if reasons:
        return reasons
    if normalize_contract_name(payload) != USER_JOURNEY_AUDIT_CONTRACT:
        reasons.append(f"user journey audit receipt contract_name must be {USER_JOURNEY_AUDIT_CONTRACT}: {audit_path}")
    if not status_pass(payload.get("status")):
        reasons.append(f"user journey audit receipt status is not pass/passed/ready: {audit_path}")
    listed_reasons = [str(item or "").strip() for item in json_list(payload.get("reasons")) if str(item or "").strip()]
    if listed_reasons:
        reasons.append(f"user journey audit receipt still reports reasons: {'; '.join(listed_reasons)}")
    if payload.get("linux_binary_under_test") is not True:
        reasons.append(f"user journey audit receipt must declare linux_binary_under_test=true: {audit_path}")
    if payload.get("used_internal_apis") is not False:
        reasons.append(f"user journey audit receipt must declare used_internal_apis=false: {audit_path}")
    if payload.get("fix_shard_separate") is not True:
        reasons.append(f"user journey audit receipt must prove tester/fixer shard separation: {audit_path}")
    if int(payload.get("open_blocking_findings_count") or 0) != 0:
        reasons.append(f"user journey audit receipt reports open blocking findings: {audit_path}")

    evidence = payload.get("evidence") or {}
    if normalize_path_string(trace_path) != str((evidence or {}).get("trace_path") or "").strip():
        reasons.append(f"user journey audit receipt trace_path does not match the trace under test: {audit_path}")
    if normalize_path_string(screenshot_dir) != str((evidence or {}).get("screenshot_dir") or "").strip():
        reasons.append(f"user journey audit receipt screenshot_dir does not match the screenshot root under test: {audit_path}")
    if not status_pass((evidence or {}).get("linux_gate_status")):
        reasons.append(f"user journey audit receipt linux_gate_status is not pass/passed/ready: {audit_path}")
    if json_list((evidence or {}).get("missing_workflows")):
        reasons.append(f"user journey audit receipt reports missing workflows: {audit_path}")
    if json_list((evidence or {}).get("nonpassing_workflows")):
        reasons.append(f"user journey audit receipt reports nonpassing workflows: {audit_path}")
    if json_list((evidence or {}).get("insufficient_screenshot_workflows")):
        reasons.append(f"user journey audit receipt reports workflows with insufficient screenshots: {audit_path}")
    missing_assertion_workflows = (evidence or {}).get("missing_assertion_workflows") or {}
    if isinstance(missing_assertion_workflows, dict) and any(missing_assertion_workflows.values()):
        reasons.append(f"user journey audit receipt reports missing user-observable assertions: {audit_path}")
    required_workflows = json_list((evidence or {}).get("required_workflows"))
    if sorted(str(item) for item in required_workflows) != sorted(USER_JOURNEY_REQUIRED_WORKFLOW_ASSERTIONS):
        reasons.append(f"user journey audit receipt required_workflows drifted from the parity gate contract: {audit_path}")
    required_assertions = (evidence or {}).get("required_workflow_assertions") or {}
    for workflow_id, expected_assertions in USER_JOURNEY_REQUIRED_WORKFLOW_ASSERTIONS.items():
        actual_assertions = required_assertions.get(workflow_id) if isinstance(required_assertions, dict) else None
        if list(actual_assertions or []) != list(expected_assertions):
            reasons.append(f"user journey audit receipt assertion contract drifted for {workflow_id}: {audit_path}")
    for workflow_row in json_list((evidence or {}).get("workflows")):
        if not isinstance(workflow_row, dict):
            reasons.append(f"user journey audit receipt contains a non-object workflow row: {audit_path}")
            continue
        workflow_id = str(workflow_row.get("id") or "").strip()
        screenshot_reviews = json_list(workflow_row.get("screenshotReview"))
        if len(screenshot_reviews) < 2:
            reasons.append(f"user journey audit receipt screenshotReview is too small for {workflow_id or 'unknown workflow'}: {audit_path}")
            continue
        for review in screenshot_reviews:
            if not isinstance(review, dict):
                reasons.append(f"user journey audit receipt contains a non-object screenshot review for {workflow_id}: {audit_path}")
                continue
            if review.get("exists") is not True:
                reasons.append(f"user journey audit receipt contains a missing screenshot for {workflow_id}: {audit_path}")
            if review.get("within_repo_root") is not True:
                reasons.append(f"user journey audit receipt contains a screenshot outside repo root for {workflow_id}: {audit_path}")
            if review.get("is_png") is not True:
                reasons.append(f"user journey audit receipt contains a non-PNG screenshot for {workflow_id}: {audit_path}")
            if not str(review.get("sha256") or "").strip():
                reasons.append(f"user journey audit receipt contains a screenshot without sha256 for {workflow_id}: {audit_path}")
    return reasons


def validate_desktop_workflow_parity_receipt(receipt_path: Path) -> list[str]:
    payload, reasons = try_load_json(receipt_path)
    if reasons:
        return reasons
    if normalize_contract_name(payload) != CHUMMER5A_DESKTOP_WORKFLOW_PARITY_CONTRACT:
        reasons.append(
            f"desktop workflow parity receipt contract_name must be {CHUMMER5A_DESKTOP_WORKFLOW_PARITY_CONTRACT}: {receipt_path}"
        )
    if not status_pass(payload.get("status")):
        reasons.append(f"desktop workflow parity receipt status is not pass/passed/ready: {receipt_path}")
    listed_reasons = [str(item or "").strip() for item in json_list(payload.get("reasons")) if str(item or "").strip()]
    if listed_reasons:
        reasons.append(f"desktop workflow parity receipt still reports reasons: {'; '.join(listed_reasons)}")
    for review_name in ("workflowFamilyReview", "recursiveWorkflowGateReview", "checklistCoverageReview"):
        review_payload = payload.get(review_name) or {}
        if not status_pass((review_payload or {}).get("status")):
            reasons.append(f"desktop workflow parity receipt {review_name} is not pass/passed/ready: {receipt_path}")
    failure_count = int(((payload.get("evidence") or {}).get("failureCount")) or 0)
    if failure_count != 0:
        reasons.append(f"desktop workflow parity receipt evidence.failureCount is non-zero: {receipt_path}")
    return reasons


def validate_generated_dialog_element_parity_receipt(receipt_path: Path) -> list[str]:
    payload, reasons = try_load_json(receipt_path)
    if reasons:
        return reasons
    if normalize_contract_name(payload) != GENERATED_DIALOG_ELEMENT_PARITY_CONTRACT:
        reasons.append(
            f"generated dialog element parity receipt contract_name must be {GENERATED_DIALOG_ELEMENT_PARITY_CONTRACT}: {receipt_path}"
        )
    if not status_pass(payload.get("status")):
        reasons.append(f"generated dialog element parity receipt status is not pass/passed/ready: {receipt_path}")
    listed_reasons = [str(item or "").strip() for item in json_list(payload.get("reasons")) if str(item or "").strip()]
    if listed_reasons:
        reasons.append(f"generated dialog element parity receipt still reports reasons: {'; '.join(listed_reasons)}")
    for review_name in ("inventoryReview", "executionReview", "verifyWiringReview"):
        review_payload = payload.get(review_name) or {}
        if not status_pass((review_payload or {}).get("status")):
            reasons.append(f"generated dialog element parity receipt {review_name} is not pass/passed/ready: {receipt_path}")
    return reasons


def validate_ui_element_parity_audit_receipt(receipt_path: Path) -> list[str]:
    payload, reasons = try_load_json(receipt_path)
    if reasons:
        return reasons
    if not status_pass(payload.get("status")):
        reasons.append(f"UI element parity audit status is not pass/passed/ready: {receipt_path}")
    summary = payload.get("summary") or {}
    total_elements = int((summary or {}).get("total_elements") or 0)
    if total_elements <= 0:
        reasons.append(f"UI element parity audit did not publish any audited elements: {receipt_path}")
    coverage_gap_keys = [str(item or "").strip() for item in json_list((summary or {}).get("coverage_gap_keys")) if str(item or "").strip()]
    if coverage_gap_keys:
        reasons.append(f"UI element parity audit still reports coverage gaps: {', '.join(coverage_gap_keys)}")
    if int((summary or {}).get("behavioral_no_count") or 0) != 0:
        reasons.append(f"UI element parity audit still reports behavioral gaps: {receipt_path}")
    if int((summary or {}).get("visual_no_count") or 0) != 0:
        reasons.append(f"UI element parity audit still reports visual gaps: {receipt_path}")
    return reasons


def validate_workflow_parity_receipt(receipt_path: Path, workflow_family_id: str, executed_receipt_path: Path) -> list[str]:
    payload, reasons = try_load_json(receipt_path)
    if reasons:
        return reasons
    if normalize_contract_name(payload) != WORKFLOW_VERIFICATION_CONTRACT:
        reasons.append(f"workflow parity receipt contract_name must be {WORKFLOW_VERIFICATION_CONTRACT}: {receipt_path}")
    if not status_pass(payload.get("status")):
        reasons.append(f"workflow parity receipt status is not pass/passed/ready: {receipt_path}")
    if not str(payload.get("summary") or "").strip():
        reasons.append(f"workflow parity receipt summary is missing for {workflow_family_id}: {receipt_path}")
    listed_reasons = [str(item or "").strip() for item in json_list(payload.get("reasons")) if str(item or "").strip()]
    if listed_reasons:
        reasons.append(f"workflow parity receipt still reports reasons for {workflow_family_id}: {'; '.join(listed_reasons)}")
    evidence = payload.get("evidence") or {}
    if str((evidence or {}).get("edition") or "").strip() != "sr6":
        reasons.append(f"workflow parity receipt edition must be sr6 for {workflow_family_id}: {receipt_path}")
    if str((evidence or {}).get("familyId") or "").strip() != workflow_family_id:
        reasons.append(f"workflow parity receipt familyId mismatch for {workflow_family_id}: {receipt_path}")
    if not str((evidence or {}).get("proofKind") or "").strip():
        reasons.append(f"workflow parity receipt proofKind is missing for {workflow_family_id}: {receipt_path}")
    audit_tests = json_list((evidence or {}).get("auditTests"))
    if not audit_tests:
        reasons.append(f"workflow parity receipt has no auditTests for {workflow_family_id}: {receipt_path}")
    execution_receipts = [str(item or "").strip() for item in json_list((evidence or {}).get("executionReceipts")) if str(item or "").strip()]
    if normalize_path_string(executed_receipt_path) not in execution_receipts:
        reasons.append(f"workflow parity receipt does not point at the canonical executed receipt for {workflow_family_id}: {receipt_path}")
    if json_list((evidence or {}).get("executionFailures")):
        reasons.append(f"workflow parity receipt reports execution failures for {workflow_family_id}: {receipt_path}")
    if json_list((evidence or {}).get("executionExternalBlockers")):
        reasons.append(f"workflow parity receipt reports execution external blockers for {workflow_family_id}: {receipt_path}")
    return reasons


def validate_executed_workflow_receipt(receipt_path: Path, workflow_family_id: str) -> list[str]:
    payload, reasons = try_load_json(receipt_path)
    if reasons:
        return reasons
    if normalize_contract_name(payload) != EXECUTED_WORKFLOW_CONTRACT:
        reasons.append(f"executed workflow receipt contract_name must be {EXECUTED_WORKFLOW_CONTRACT}: {receipt_path}")
    if not status_pass(payload.get("status")):
        reasons.append(f"executed workflow receipt status is not pass/passed/ready: {receipt_path}")
    if not str(payload.get("summary") or "").strip():
        reasons.append(f"executed workflow receipt summary is missing for {workflow_family_id}: {receipt_path}")
    listed_reasons = [str(item or "").strip() for item in json_list(payload.get("reasons")) if str(item or "").strip()]
    if listed_reasons:
        reasons.append(f"executed workflow receipt still reports reasons for {workflow_family_id}: {'; '.join(listed_reasons)}")
    evidence = payload.get("evidence") or {}
    if str((evidence or {}).get("edition") or "").strip() != "sr6":
        reasons.append(f"executed workflow receipt edition must be sr6 for {workflow_family_id}: {receipt_path}")
    if str((evidence or {}).get("familyId") or "").strip() != workflow_family_id:
        reasons.append(f"executed workflow receipt familyId mismatch for {workflow_family_id}: {receipt_path}")
    if not str((evidence or {}).get("proofKind") or "").strip():
        reasons.append(f"executed workflow receipt proofKind is missing for {workflow_family_id}: {receipt_path}")
    dotnet_test = (evidence or {}).get("dotnetTest") or {}
    if int((dotnet_test or {}).get("exitCode") or 0) != 0:
        reasons.append(f"executed workflow receipt dotnetTest.exitCode is non-zero for {workflow_family_id}: {receipt_path}")
    matched_passed_tests = (evidence or {}).get("matchedPassedTests") or []
    if not isinstance(matched_passed_tests, list) or not matched_passed_tests:
        reasons.append(f"executed workflow receipt has no matchedPassedTests for {workflow_family_id}: {receipt_path}")
    missing_audit_tests = (evidence or {}).get("missingAuditTests") or []
    if isinstance(missing_audit_tests, list) and missing_audit_tests:
        reasons.append(
            f"executed workflow receipt is missing audit tests for {workflow_family_id}: {', '.join(str(item) for item in missing_audit_tests)}"
        )
    failed_audit_tests = (evidence or {}).get("failedAuditTests") or {}
    if isinstance(failed_audit_tests, dict) and failed_audit_tests:
        reasons.append(f"executed workflow receipt has failed audit tests for {workflow_family_id}: {receipt_path}")
    external_blocker = str((evidence or {}).get("external_blocker") or "").strip()
    if external_blocker:
        reasons.append(f"executed workflow receipt still reports external_blocker={external_blocker}: {receipt_path}")
    return reasons


def reconstruction_receipt_name(fixture_name: str) -> str:
    return f"{fixture_name}.generated.json"


def validate_fixture_reconstruction_receipt(receipt_path: Path, fixture: FixtureSpec) -> list[str]:
    payload, reasons = try_load_json(receipt_path)
    if reasons:
        return reasons
    if normalize_contract_name(payload) != FIXTURE_RECONSTRUCTION_CONTRACT:
        reasons.append(f"fixture reconstruction receipt contract_name must be {FIXTURE_RECONSTRUCTION_CONTRACT}: {receipt_path}")
    if not status_pass(payload.get("status")):
        reasons.append(f"fixture reconstruction receipt status is not pass/passed/ready: {receipt_path}")
    if str(payload.get("fixtureName") or "").strip() != fixture.fixture_name:
        reasons.append(f"fixture reconstruction receipt fixtureName mismatch for {fixture.fixture_name}: {receipt_path}")
    if str(payload.get("characterName") or "").strip() != fixture.character_name:
        reasons.append(f"fixture reconstruction receipt characterName mismatch for {fixture.fixture_name}: {receipt_path}")
    if payload.get("linux_binary_under_test") is not True:
        reasons.append(f"fixture reconstruction receipt must declare linux_binary_under_test=true for {fixture.fixture_name}: {receipt_path}")
    if payload.get("used_internal_apis") is not False:
        reasons.append(f"fixture reconstruction receipt must declare used_internal_apis=false for {fixture.fixture_name}: {receipt_path}")
    screenshots = json_list(payload.get("screenshots"))
    if len(screenshots) < 4:
        reasons.append(f"fixture reconstruction receipt must publish at least four screenshots for {fixture.fixture_name}: {receipt_path}")
    else:
        for raw_screenshot in screenshots:
            screenshot_path = Path(str(raw_screenshot))
            if not screenshot_path.is_absolute():
                screenshot_path = receipt_path.parent / screenshot_path
            if not screenshot_path.is_file():
                reasons.append(f"fixture reconstruction receipt is missing screenshot {screenshot_path}")
    evidence = payload.get("evidence") or {}
    output_artifact_keys = (
        "savedFilePath",
        "exportFilePath",
        "printPreviewFilePath",
        "pdfArtifactPath",
    )
    for artifact_key in output_artifact_keys:
        artifact_path_raw = str((evidence or {}).get(artifact_key) or "").strip()
        if not artifact_path_raw:
            reasons.append(f"fixture reconstruction receipt is missing evidence.{artifact_key} for {fixture.fixture_name}: {receipt_path}")
            continue
        artifact_path = Path(artifact_path_raw)
        if not artifact_path.is_file():
            reasons.append(f"fixture reconstruction receipt is missing output artifact {artifact_path} for {fixture.fixture_name}")
    print_preview_path_raw = str((evidence or {}).get("printPreviewFilePath") or "").strip()
    if print_preview_path_raw:
        print_preview_path = Path(print_preview_path_raw)
        if print_preview_path.is_file():
            print_preview = print_preview_path.read_text(encoding="utf-8", errors="ignore")
            if "<html" not in print_preview.lower():
                reasons.append(f"fixture reconstruction print preview does not look like HTML for {fixture.fixture_name}: {print_preview_path}")
    pdf_artifact_path_raw = str((evidence or {}).get("pdfArtifactPath") or "").strip()
    if pdf_artifact_path_raw:
        pdf_artifact_path = Path(pdf_artifact_path_raw)
        if pdf_artifact_path.is_file():
            pdf_bytes = pdf_artifact_path.read_bytes()
            if not pdf_bytes.startswith(b"%PDF-"):
                reasons.append(f"fixture reconstruction PDF artifact does not have a PDF header for {fixture.fixture_name}: {pdf_artifact_path}")
    assertions = payload.get("assertions") or {}
    required_assertions = (
        "openedByUi",
        "savedByUi",
        "exportedByUi",
        "printedByUi",
        "pdfArtifactProducedByUiPrintRoute",
        "outputArtifactsProducedByUi",
        "reloadedByUi",
        "roundTripPreservedIdentity",
    )
    missing_assertions = [
        assertion
        for assertion in required_assertions
        if not isinstance(assertions, dict) or assertions.get(assertion) is not True
    ]
    if missing_assertions:
        reasons.append(
            f"fixture reconstruction receipt is missing required assertion(s) for {fixture.fixture_name}: {', '.join(missing_assertions)}"
        )
    return reasons


def event_log(path: Path, event: dict[str, Any]) -> None:
    with path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(event, sort_keys=True) + "\n")


def build_baseline_lookup(oracle_baselines: dict[str, Any]) -> dict[str, dict[str, Any]]:
    baselines = oracle_baselines.get("screenshot_baselines") or []
    return {
        str(item["id"]): item
        for item in baselines
        if isinstance(item, dict) and "id" in item
    }


def build_screenshot_entry_lookup(screenshot_evidence: dict[str, Any]) -> dict[str, dict[str, Any]]:
    entries = screenshot_evidence.get("entries") or []
    return {
        str(item["screenshot"]): item
        for item in entries
        if isinstance(item, dict) and "screenshot" in item
    }


def build_workflow_coverage_lookup(screenshot_evidence: dict[str, Any]) -> dict[str, dict[str, Any]]:
    coverage = screenshot_evidence.get("workflowCoverage") or []
    return {
        str(item["workflowFamilyId"]): item
        for item in coverage
        if isinstance(item, dict) and "workflowFamilyId" in item
    }


def render_failure_markdown(failures: list[Failure]) -> list[str]:
    if not failures:
        return ["# Failures", "", "No blocking failures detected."]
    lines = ["# Failures", ""]
    for failure in failures:
        lines.extend(
            [
                f"## Failure: {failure.fixture} / {failure.checkpoint}",
                "",
                f"- Fixture: {failure.fixture}",
                f"- Step: {failure.step}",
                f"- Action: {failure.checkpoint}",
                f"- Expected: {failure.expected}",
                f"- Actual: {failure.actual}",
                f"- Severity: {failure.severity}",
                f"- Category: {failure.category}",
                f"- Reference screenshot: {failure.reference_screenshot}",
                f"- Actual screenshot: {failure.actual_screenshot}",
                f"- Diff screenshot: {failure.diff_screenshot}",
                "",
                "### Remediation Target",
                "",
                failure.remediation_target,
                "",
            ]
        )
    return lines


def landmark_present(entry: dict[str, Any], landmark: str) -> bool:
    aliases = LANDMARK_ALIASES.get(landmark, (landmark,))
    haystack_parts: list[str] = []
    for key in ("dialogTitle", "dialogMessage", "previewText"):
        value = entry.get(key)
        if isinstance(value, str):
            haystack_parts.append(value)
    for key in ("visibleTextSamples", "visibleMenuCommandIds", "visibleTabLabels", "visibleSectionQuickActionIds", "visibleNamedControlIds", "selectedListRowTexts"):
        values = entry.get(key) or []
        if isinstance(values, list):
            haystack_parts.extend(str(value) for value in values)
    haystack = normalize_text(" ".join(haystack_parts))
    return any(normalize_text(alias) in haystack for alias in aliases)


def copy_screenshot(source: Path, destination_root: Path) -> Path:
    ensure_dir(destination_root)
    destination = destination_root / source.name
    if source.is_file():
        shutil.copy2(source, destination)
    return destination


def resolve_screenshot_path(roots: list[Path], screenshot_name: str) -> Path:
    for root in roots:
        if not str(root):
            continue
        candidate = root / screenshot_name
        if candidate.is_file():
            return candidate
    primary_root = roots[0] if roots else Path()
    return primary_root / screenshot_name


def write_sentinel(path: Path, lines: list[str]) -> Path:
    ensure_dir(path.parent)
    path.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8")
    return path


def run_gate(args: argparse.Namespace) -> int:
    artifacts = init_artifacts(args.artifacts)
    write_sentinel(
        artifacts.screenshots_reference / REFERENCE_SENTINEL,
        [
            "Legacy reference screenshots were not present for this run.",
            "The tester used Chummer5a structural oracle sources plus published Chummer6 screenshot/control evidence instead.",
        ],
    )
    write_sentinel(
        artifacts.screenshots_diff / DIFF_SENTINEL,
        [
            "Pixel diff images were not generated for this run.",
            "The tester used element-aware structural checks backed by screenshot control evidence.",
        ],
    )

    run_started_at = now_iso()
    failures: list[Failure] = []
    reconstruction_log_lines = [f"[{run_started_at}] start Chummer5a parity tester"]
    event_log(artifacts.ui_actions_trace, {"ts": run_started_at, "phase": "start", "action": "initialize", "artifacts": str(artifacts.root)})

    required_paths = (
        args.fixture_inventory,
        args.oracle_baselines,
        args.workflow_pack,
        args.screenshot_evidence,
        args.visual_gate,
        args.flagship_gate,
        args.executable_gate,
        args.workflow_parity_dir,
        args.executed_workflow_parity_dir,
        args.user_journey_trace,
        args.user_journey_audit,
    )
    missing_paths = [str(path) for path in required_paths if not Path(path).exists()]
    if missing_paths:
        metadata = {
            "startedAt": run_started_at,
            "completedAt": now_iso(),
            "status": "infrastructure-failure",
            "summary": "Parity tester inputs are missing.",
            "missingPaths": missing_paths,
        }
        write_json(artifacts.run_metadata, metadata)
        write_lines(artifacts.failures_md, ["# Failures", "", "Infrastructure failure: required inputs are missing."])
        write_json(artifacts.failures_json, {"failures": [], "missingPaths": missing_paths})
        write_lines(artifacts.coverage_matrix, ["# Coverage Matrix", "", "Infrastructure failure prevented coverage generation."])
        reconstruction_log_lines.extend(f"missing input: {path}" for path in missing_paths)
        write_lines(artifacts.reconstruction_log, reconstruction_log_lines)
        return INFRA_EXIT

    chummer5a_repo = Path(args.chummer5a_repo).expanduser().resolve()
    if not chummer5a_repo.is_dir():
        raise FileNotFoundError(f"Chummer5a repo path does not exist: {chummer5a_repo}")
    chummer5a_commit = git_rev_parse(chummer5a_repo, args.chummer5a_ref)
    available_fixture_total = available_fixture_count(chummer5a_repo)
    event_log(artifacts.ui_actions_trace, {"ts": now_iso(), "phase": "observe", "action": "pin_chummer5a_repo", "repo": str(chummer5a_repo), "commit": chummer5a_commit})

    fixture_inventory = load_yaml(args.fixture_inventory)
    oracle_baselines = load_yaml(args.oracle_baselines)
    workflow_pack = load_yaml(args.workflow_pack)
    screenshot_evidence = load_json(args.screenshot_evidence)
    visual_gate = load_json(args.visual_gate)
    flagship_gate = load_json(args.flagship_gate)
    executable_gate = load_json(args.executable_gate)
    baseline_lookup = build_baseline_lookup(oracle_baselines)
    screenshot_entry_lookup = build_screenshot_entry_lookup(screenshot_evidence)
    workflow_coverage_lookup = build_workflow_coverage_lookup(screenshot_evidence)
    declared_screenshot_root = Path(screenshot_evidence.get("screenshotDirectory") or "").expanduser()
    screenshot_roots = [declared_screenshot_root]
    sibling_screenshot_root = args.screenshot_evidence.parent
    if sibling_screenshot_root not in screenshot_roots:
        screenshot_roots.append(sibling_screenshot_root)

    if args.fixture:
        fixtures = select_user_fixtures(args.fixture)
    else:
        fixtures = select_default_fixtures(chummer5a_repo)
    fixture_scope = classify_fixture_scope(
        explicit_fixture_paths=args.fixture,
        selected_fixture_count=len(fixtures),
        available_fixture_total=available_fixture_total,
    )
    event_log(
        artifacts.ui_actions_trace,
        {
            "ts": now_iso(),
            "phase": "observe",
            "action": "select_fixtures",
            "fixtures": [fixture.fixture_name for fixture in fixtures],
            "fixtureScope": fixture_scope,
        },
    )
    required_workflow_family_ids = sorted({family_id for fixture in fixtures for family_id in fixture.workflow_family_ids})
    selected_fixture_ui_certified = False
    full_fixture_ui_certified = False
    recursive_workflow_parity_failures = validate_desktop_workflow_parity_receipt(args.desktop_workflow_parity_receipt)
    generated_dialog_element_parity_failures = validate_generated_dialog_element_parity_receipt(
        args.generated_dialog_element_parity_receipt
    )
    ui_element_parity_audit_failures = validate_ui_element_parity_audit_receipt(
        args.ui_element_parity_audit_receipt
    )
    recursive_settings_and_elements_certified = not (
        recursive_workflow_parity_failures
        or generated_dialog_element_parity_failures
        or ui_element_parity_audit_failures
    )

    gate_receipts = {
        "desktop_executable_exit_gate": executable_gate,
        "desktop_visual_familiarity_exit_gate": visual_gate,
        "ui_flagship_release_gate": flagship_gate,
    }
    for gate_name, payload in gate_receipts.items():
        if not status_pass(payload.get("status")):
            step = len(failures) + 1
            failures.append(
                Failure(
                    fixture="(fleet proof)",
                    checkpoint=gate_name,
                    step=step,
                    category="behavioral",
                    severity="blocking",
                    expected=f"{gate_name} should be passing before parity exit can clear.",
                    actual=f"{gate_name} status was {payload.get('status')!r}.",
                    reference_screenshot=str(artifacts.screenshots_reference / REFERENCE_SENTINEL),
                    actual_screenshot="",
                    diff_screenshot=str(artifacts.screenshots_diff / DIFF_SENTINEL),
                    remediation_target=f"Repair the underlying published gate and rerun the parity tester after {gate_name} is green again.",
                )
            )

    user_journey_failures = validate_user_journey_trace(
        args.user_journey_trace,
        args.user_journey_screenshot_dir,
    )
    if user_journey_failures:
        step = len(failures) + 1
        failures.append(
            Failure(
                fixture="(live user journey)",
                checkpoint="user_journey_tester_trace",
                step=step,
                category="behavioral",
                severity="blocking",
                expected="User-journey trace should prove observable Linux desktop flows with the required assertions and screenshots.",
                actual="; ".join(user_journey_failures),
                reference_screenshot=str(artifacts.screenshots_reference / REFERENCE_SENTINEL),
                actual_screenshot="",
                diff_screenshot=str(artifacts.screenshots_diff / DIFF_SENTINEL),
                remediation_target="Refresh the user-journey tester trace until the required Linux desktop workflows, screenshots, and assertions all pass.",
            )
        )
    user_journey_audit_failures = validate_user_journey_audit_receipt(
        args.user_journey_audit,
        args.user_journey_trace,
        args.user_journey_screenshot_dir,
    )
    if user_journey_audit_failures:
        step = len(failures) + 1
        failures.append(
            Failure(
                fixture="(user journey audit)",
                checkpoint="user_journey_tester_audit",
                step=step,
                category="behavioral",
                severity="blocking",
                expected="User-journey audit receipt should independently certify the Linux desktop proof trace with no remaining reasons or contract drift.",
                actual="; ".join(user_journey_audit_failures),
                reference_screenshot=str(artifacts.screenshots_reference / REFERENCE_SENTINEL),
                actual_screenshot="",
                diff_screenshot=str(artifacts.screenshots_diff / DIFF_SENTINEL),
                remediation_target="Refresh the separate user-journey tester audit until it passes against the same trace and screenshot set the parity gate is reading.",
            )
        )

    for workflow_family_id in required_workflow_family_ids:
        workflow_receipt_path = args.workflow_parity_dir / f"{workflow_family_id}.generated.json"
        executed_receipt_path = args.executed_workflow_parity_dir / f"{workflow_family_id}.generated.json"
        workflow_parity_failures = validate_workflow_parity_receipt(
            workflow_receipt_path,
            workflow_family_id,
            executed_receipt_path,
        )
        if workflow_parity_failures:
            step = len(failures) + 1
            failures.append(
                Failure(
                    fixture="(workflow parity proof)",
                    checkpoint=f"workflow::{workflow_family_id}",
                    step=step,
                    category="behavioral",
                    severity="blocking",
                    expected=f"Workflow parity receipt for {workflow_family_id} should be canonical, passing, and tied to the executed receipt.",
                    actual="; ".join(workflow_parity_failures),
                    reference_screenshot=str(artifacts.screenshots_reference / REFERENCE_SENTINEL),
                    actual_screenshot="",
                    diff_screenshot=str(artifacts.screenshots_diff / DIFF_SENTINEL),
                    remediation_target=f"Refresh the workflow verification proof for {workflow_family_id} until the canonical SR6 receipt passes and points at the executed family receipt.",
                )
            )
        executed_failures = validate_executed_workflow_receipt(executed_receipt_path, workflow_family_id)
        if executed_failures:
            step = len(failures) + 1
            failures.append(
                Failure(
                    fixture="(executed workflow proof)",
                    checkpoint=f"executed::{workflow_family_id}",
                    step=step,
                    category="behavioral",
                    severity="blocking",
                    expected=f"Executed workflow receipt for {workflow_family_id} should be present and passing.",
                    actual="; ".join(executed_failures),
                    reference_screenshot=str(artifacts.screenshots_reference / REFERENCE_SENTINEL),
                    actual_screenshot="",
                    diff_screenshot=str(artifacts.screenshots_diff / DIFF_SENTINEL),
                    remediation_target=f"Refresh the executed workflow proof for {workflow_family_id} until the receipt is passing and grounded in executable audit tests.",
                )
            )

    reconstruction_failures_by_fixture: list[tuple[FixtureSpec, list[str]]] = []
    missing_reconstruction_dir_reason = ""
    if args.reconstruction_receipts_dir is None:
        missing_reconstruction_dir_reason = (
            f"{len(fixtures)} fixture(s) were selected under {fixture_scope}, but "
            "--reconstruction-receipts-dir was not provided."
        )
        if not recursive_settings_and_elements_certified:
            recursive_proof_reasons = [
                *recursive_workflow_parity_failures,
                *generated_dialog_element_parity_failures,
                *ui_element_parity_audit_failures,
            ]
            actual = missing_reconstruction_dir_reason
            if recursive_proof_reasons:
                actual += " Exhaustive recursive settings/element proof is also unavailable: " + "; ".join(
                    recursive_proof_reasons
                )
            step = len(failures) + 1
            failures.append(
                Failure(
                    fixture=", ".join(fixture.fixture_name for fixture in fixtures),
                    checkpoint="fixture_ui_reconstruction_receipts",
                    step=step,
                    category="behavioral",
                    severity="blocking",
                    expected="Selected fixture sets should carry per-fixture UI reconstruction receipts.",
                    actual=actual,
                    reference_screenshot=str(artifacts.screenshots_reference / REFERENCE_SENTINEL),
                    actual_screenshot="",
                    diff_screenshot=str(artifacts.screenshots_diff / DIFF_SENTINEL),
                    remediation_target="Generate per-fixture UI reconstruction receipts proving open, save-as, export, print-preview, PDF-route artifacts, reload, and identity-preserving roundtrips for the selected fixture set.",
                )
            )
    else:
        for fixture in fixtures:
            receipt_path = args.reconstruction_receipts_dir / reconstruction_receipt_name(fixture.fixture_name)
            reconstruction_failures = validate_fixture_reconstruction_receipt(receipt_path, fixture)
            if reconstruction_failures:
                reconstruction_failures_by_fixture.append((fixture, reconstruction_failures))
        if not reconstruction_failures_by_fixture:
            selected_fixture_ui_certified = True
            full_fixture_ui_certified = fixture_scope == "all_available_fixtures_explicit"
        elif not recursive_settings_and_elements_certified:
            for fixture, reconstruction_failures in reconstruction_failures_by_fixture:
                step = len(failures) + 1
                failures.append(
                    Failure(
                        fixture=fixture.fixture_name,
                        checkpoint="fixture_ui_reconstruction_receipt",
                        step=step,
                        category="behavioral",
                        severity="blocking",
                        expected=f"{fixture.fixture_name} should have a passing per-fixture UI reconstruction receipt.",
                        actual="; ".join(reconstruction_failures),
                        reference_screenshot=str(artifacts.screenshots_reference / REFERENCE_SENTINEL),
                        actual_screenshot="",
                        diff_screenshot=str(artifacts.screenshots_diff / DIFF_SENTINEL),
                        remediation_target=f"Refresh the UI reconstruction proof for {fixture.fixture_name} until the receipt passes with open/save-as/export/print/PDF/reload assertions and screenshot evidence.",
                    )
                )

    for non_negotiable_id, asserted in (workflow_pack.get("desktop_non_negotiables_asserted") or {}).items():
        if asserted is not True:
            step = len(failures) + 1
            failures.append(
                Failure(
                    fixture="(fleet proof)",
                    checkpoint=non_negotiable_id,
                    step=step,
                    category="functional-coverage",
                    severity="blocking",
                    expected=f"Desktop non-negotiable {non_negotiable_id} should be explicitly asserted.",
                    actual=f"desktop_non_negotiables_asserted[{non_negotiable_id!r}] was {asserted!r}.",
                    reference_screenshot=str(artifacts.screenshots_reference / REFERENCE_SENTINEL),
                    actual_screenshot="",
                    diff_screenshot=str(artifacts.screenshots_diff / DIFF_SENTINEL),
                    remediation_target="Restore the missing non-negotiable assertion and its backing screenshot/workflow proof.",
                )
            )

    task_packs = workflow_pack.get("task_packs") or []
    first_minute_entries: list[dict[str, Any]] = []
    for task_pack in task_packs:
        if not isinstance(task_pack, dict):
            continue
        for baseline_id in task_pack.get("screenshot_baseline_ids") or []:
            baseline = baseline_lookup.get(str(baseline_id))
            if not baseline:
                continue
            screenshot_name = str(baseline.get("filename") or "")
            if not screenshot_name:
                continue
            entry = screenshot_entry_lookup.get(screenshot_name)
            if entry is not None:
                first_minute_entries.append(entry)
    for task_pack in task_packs:
        if not isinstance(task_pack, dict):
            continue
        task_id = str(task_pack.get("task_id") or "")
        baseline_ids = task_pack.get("screenshot_baseline_ids") or []
        landmarks = task_pack.get("landmarks") or []
        task_actual_paths: list[str] = []
        task_observed_landmarks: set[str] = set()
        task_problems: list[str] = []
        for baseline_id in baseline_ids:
            baseline = baseline_lookup.get(str(baseline_id))
            if baseline is None:
                task_problems.append(f"Baseline {baseline_id} is missing from {args.oracle_baselines}.")
                continue
            screenshot_name = str(baseline.get("filename") or "")
            if not screenshot_name:
                task_problems.append(f"Baseline {baseline_id} does not declare a screenshot filename.")
                continue
            screenshot_path = resolve_screenshot_path(screenshot_roots, screenshot_name)
            if not screenshot_path.is_file():
                task_problems.append(f"Screenshot file is missing for {screenshot_name} at {screenshot_path}.")
                continue
            copied_actual_path = copy_screenshot(screenshot_path, artifacts.screenshots_actual)
            task_actual_paths.append(str(copied_actual_path))
            entry = screenshot_entry_lookup.get(screenshot_name)
            if entry is None:
                task_problems.append(f"Screenshot control evidence is missing for {screenshot_name}.")
                continue
            for landmark in landmarks:
                if landmark_present(entry, str(landmark)):
                    task_observed_landmarks.add(str(landmark))
        for landmark in landmarks:
            landmark_text = str(landmark)
            if landmark_text in task_observed_landmarks:
                continue
            if any(landmark_present(entry, landmark_text) for entry in first_minute_entries):
                task_observed_landmarks.add(landmark_text)
        missing_task_landmarks = [str(landmark) for landmark in landmarks if str(landmark) not in task_observed_landmarks]
        if task_problems or missing_task_landmarks:
            step = len(failures) + 1
            details = [*task_problems]
            if missing_task_landmarks:
                details.append(f"Missing landmarks across task screenshots: {', '.join(missing_task_landmarks)}.")
            failures.append(
                Failure(
                    fixture="(first minute)",
                    checkpoint=task_id,
                    step=step,
                    category="visual" if missing_task_landmarks else "functional-coverage",
                    severity="blocking",
                    expected=f"Task {task_id} should cover landmarks: {', '.join(str(item) for item in landmarks)}.",
                    actual=" ".join(details),
                    reference_screenshot=str(artifacts.screenshots_reference / REFERENCE_SENTINEL),
                    actual_screenshot=", ".join(task_actual_paths),
                    diff_screenshot=str(artifacts.screenshots_diff / DIFF_SENTINEL),
                    remediation_target=f"Rework the first-minute proof for {task_id} until the screenshot set collectively exposes the required veteran landmarks.",
                )
            )

    coverage_rows: list[dict[str, Any]] = []
    for workflow_family_id in required_workflow_family_ids:
        fixtures_for_family = [fixture for fixture in fixtures if workflow_family_id in fixture.workflow_family_ids]
        workflow_receipt_path = args.workflow_parity_dir / f"{workflow_family_id}.generated.json"
        workflow_receipt = load_json(workflow_receipt_path) if workflow_receipt_path.is_file() else {}
        workflow_coverage = workflow_coverage_lookup.get(workflow_family_id, {})
        screenshot_files = [str(item) for item in (workflow_coverage.get("screenshotFiles") or [])]
        actual_paths: list[str] = []
        workflow_failures: list[str] = []
        if not workflow_receipt_path.is_file():
            workflow_failures.append(f"Missing workflow receipt: {workflow_receipt_path}")
        elif not status_pass(workflow_receipt.get("status")):
            workflow_failures.append(f"Workflow receipt status is {workflow_receipt.get('status')!r}")
        if not workflow_coverage:
            workflow_failures.append("Screenshot control evidence is missing workflow coverage")
        for screenshot_name in screenshot_files:
            screenshot_path = resolve_screenshot_path(screenshot_roots, screenshot_name)
            actual_paths.append(str(artifacts.screenshots_actual / screenshot_name))
            if not screenshot_path.is_file():
                workflow_failures.append(f"Missing workflow screenshot: {screenshot_path}")
                continue
            copy_screenshot(screenshot_path, artifacts.screenshots_actual)
            if screenshot_name not in screenshot_entry_lookup:
                workflow_failures.append(f"Missing control-evidence entry for screenshot: {screenshot_name}")
        if workflow_failures:
            step = len(failures) + 1
            failures.append(
                Failure(
                    fixture=", ".join(fixture.fixture_name for fixture in fixtures_for_family),
                    checkpoint=workflow_family_id,
                    step=step,
                    category="functional-coverage",
                    severity="blocking",
                    expected=f"Workflow family {workflow_family_id} should have passing receipt and screenshot coverage.",
                    actual="; ".join(workflow_failures),
                    reference_screenshot=str(artifacts.screenshots_reference / REFERENCE_SENTINEL),
                    actual_screenshot=", ".join(actual_paths),
                    diff_screenshot=str(artifacts.screenshots_diff / DIFF_SENTINEL),
                    remediation_target=f"Rework the client workflow or its published proof until {workflow_family_id} is fully covered and passing.",
                )
            )
        coverage_rows.append(
            {
                "feature": workflow_family_id,
                "fixtures": [fixture.fixture_name for fixture in fixtures_for_family],
                "ui_path": workflow_coverage.get("legacyBehaviorLineage") or "published workflow parity receipt",
                "checkpoints": screenshot_files,
                "result": "pass" if not workflow_failures else "fail",
            }
        )
        event_log(
            artifacts.ui_actions_trace,
            {
                "ts": now_iso(),
                "phase": "verify",
                "action": "check_workflow_family",
                "workflowFamilyId": workflow_family_id,
                "fixtures": [fixture.fixture_name for fixture in fixtures_for_family],
                "status": "pass" if not workflow_failures else "fail",
                "checkpoints": screenshot_files,
                "failures": workflow_failures,
            },
        )

    coverage_lines = [
        "# Coverage Matrix",
        "",
        "| Chummer5a feature | Fixture(s) exercising it | Chummer6 UI path | Screenshot checkpoints | Result |",
        "| --- | --- | --- | --- | --- |",
    ]
    for row in coverage_rows:
        coverage_lines.append(
            "| {feature} | {fixtures} | {ui_path} | {checkpoints} | {result} |".format(
                feature=row["feature"],
                fixtures=", ".join(row["fixtures"]),
                ui_path=str(row["ui_path"]).replace("|", "\\|"),
                checkpoints=", ".join(row["checkpoints"]) or "(none)",
                result=row["result"],
            )
        )
    write_lines(artifacts.coverage_matrix, coverage_lines)

    failures_payload = {
        "failures": [
            {
                "fixture": failure.fixture,
                "checkpoint": failure.checkpoint,
                "step": failure.step,
                "category": failure.category,
                "severity": failure.severity,
                "expected": failure.expected,
                "actual": failure.actual,
                "reference_screenshot": failure.reference_screenshot,
                "actual_screenshot": failure.actual_screenshot,
                "diff_screenshot": failure.diff_screenshot,
                "remediation_target": failure.remediation_target,
            }
            for failure in failures
        ]
    }
    write_json(artifacts.failures_json, failures_payload)
    write_lines(artifacts.failures_md, render_failure_markdown(failures))

    summary = (
        "Milestone exit rejected: Chummer6 is not yet practically identical to Chummer5a for the tested workflow. "
        "See failures.md and screenshot diffs for required corrections."
        if failures
        else pass_summary_for_scope(
            fixture_scope,
            selected_fixture_ui_certified=selected_fixture_ui_certified,
            recursive_settings_and_elements_certified=recursive_settings_and_elements_certified,
        )
    )
    metadata = {
        "startedAt": run_started_at,
        "completedAt": now_iso(),
        "status": "fail" if failures else "pass",
        "summary": summary,
        "mode": "proof_backed_structural_oracle",
        "proofScope": {
            "fixtureScope": fixture_scope,
            "selectedFixtureCount": len(fixtures),
            "availableFixtureCount": available_fixture_total,
            "uiReconstructionExecuted": selected_fixture_ui_certified,
            "perFixtureRoundTripExecuted": selected_fixture_ui_certified,
            "perFixtureOutputRoutesExecuted": selected_fixture_ui_certified,
            "perFixturePdfArtifactsProduced": selected_fixture_ui_certified,
            "legacyReferenceScreenshotsCaptured": False,
            "pixelDiffsGenerated": False,
            "certifiesSelectedFixturesCanBeRebuiltOnlyUsingUi": selected_fixture_ui_certified,
            "certifiesEveryFixtureCanBeRebuiltOnlyUsingUi": full_fixture_ui_certified,
            "recursiveSettingsAndElementsCertified": recursive_settings_and_elements_certified,
            "desktopWorkflowParityCertified": not recursive_workflow_parity_failures,
            "generatedDialogElementParityCertified": not generated_dialog_element_parity_failures,
            "uiElementParityAuditCertified": not ui_element_parity_audit_failures,
        },
        "proofClaims": proof_claims(
            selected_fixture_ui_certified=selected_fixture_ui_certified,
            recursive_settings_and_elements_certified=recursive_settings_and_elements_certified,
        ),
        "proofLimitations": proof_limitations(
            selected_fixture_ui_certified=selected_fixture_ui_certified,
            full_fixture_ui_certified=full_fixture_ui_certified,
            recursive_settings_and_elements_certified=recursive_settings_and_elements_certified,
        ),
        "resolution": args.resolution,
        "scale": args.scale,
        "chummer5aRepo": str(chummer5a_repo),
        "chummer5aRepoDefaultSource": DEFAULT_CHUMMER5A_REPO_SOURCE,
        "chummer5aRepoPathSource": "cli_argument" if chummer5a_repo_arg_provided() else DEFAULT_CHUMMER5A_REPO_SOURCE,
        "chummer5aRef": args.chummer5a_ref,
        "chummer5aCommit": chummer5a_commit,
        "selectedFixtures": [
            {
                "categoryId": fixture.category_id,
                "categoryLabel": fixture.category_label,
                "fixtureName": fixture.fixture_name,
                "characterName": fixture.character_name,
                "path": str(fixture.file_path),
                "featureCounts": fixture.feature_counts,
                "workflowFamilyIds": list(fixture.workflow_family_ids),
            }
            for fixture in fixtures
        ],
        "fixtureInventoryCounts": fixture_inventory.get("counts") or {},
        "workflowFamilyCount": len(required_workflow_family_ids),
        "failureCount": len(failures),
        "evidenceSources": {
            "fixtureInventory": str(args.fixture_inventory),
            "oracleBaselines": str(args.oracle_baselines),
            "workflowPack": str(args.workflow_pack),
            "screenshotEvidence": str(args.screenshot_evidence),
            "workflowParityDir": str(args.workflow_parity_dir),
            "executedWorkflowParityDir": str(args.executed_workflow_parity_dir),
            "visualGate": str(args.visual_gate),
            "flagshipGate": str(args.flagship_gate),
            "executableGate": str(args.executable_gate),
            "userJourneyTrace": str(args.user_journey_trace),
            "userJourneyAudit": str(args.user_journey_audit),
            "userJourneyScreenshotDir": str(args.user_journey_screenshot_dir),
            "reconstructionReceiptsDir": str(args.reconstruction_receipts_dir) if args.reconstruction_receipts_dir else "",
            "desktopWorkflowParityReceipt": str(args.desktop_workflow_parity_receipt),
            "generatedDialogElementParityReceipt": str(args.generated_dialog_element_parity_receipt),
            "uiElementParityAuditReceipt": str(args.ui_element_parity_audit_receipt),
        },
        "recursiveParityReceiptReview": {
            "status": "pass" if recursive_settings_and_elements_certified else "fail",
            "summary": (
                "Recursive settings, dialog, and element parity receipts are passing."
                if recursive_settings_and_elements_certified
                else "Recursive settings, dialog, and element parity receipts are incomplete."
            ),
            "desktopWorkflowParityReasons": recursive_workflow_parity_failures,
            "generatedDialogElementParityReasons": generated_dialog_element_parity_failures,
            "uiElementParityAuditReasons": ui_element_parity_audit_failures,
        },
        "fixtureReconstructionReview": {
            "status": "pass" if selected_fixture_ui_certified else "fail",
            "summary": (
                "Selected fixtures have passing per-fixture UI reconstruction receipts."
                if selected_fixture_ui_certified
                else "Selected fixtures are relying on recursive settings/element proof instead of per-fixture UI reconstruction receipts."
                if recursive_settings_and_elements_certified
                else "Selected fixtures are missing required per-fixture UI reconstruction proof."
            ),
            "missingReconstructionDirReason": missing_reconstruction_dir_reason,
            "fixtureFailures": {
                fixture.fixture_name: reasons
                for fixture, reasons in reconstruction_failures_by_fixture
            },
        },
        "resolvedScreenshotRoots": [str(path) for path in screenshot_roots],
    }
    write_json(artifacts.run_metadata, metadata)
    reconstruction_log_lines.extend(
        [
            f"selected fixtures: {', '.join(fixture.fixture_name for fixture in fixtures)}",
            f"required workflow families: {', '.join(required_workflow_family_ids)}",
            f"result: {'fail' if failures else 'pass'}",
            f"failure count: {len(failures)}",
        ]
    )
    write_lines(artifacts.reconstruction_log, reconstruction_log_lines)
    event_log(artifacts.ui_actions_trace, {"ts": now_iso(), "phase": "finish", "action": "write_reports", "status": metadata["status"], "failureCount": len(failures)})
    return FAIL_EXIT if failures else PASS_EXIT


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Run the Chummer5a parity tester first slice against published Chummer6 desktop proof.",
    )
    parser.add_argument("--chummer5a-repo", type=Path, default=DEFAULT_CHUMMER5A_REPO, help=f"Local Chummer5a repository path (default: {DEFAULT_CHUMMER5A_REPO})")
    parser.add_argument("--chummer5a-ref", default=DEFAULT_CHUMMER5A_REF, help=f"Git ref to pin inside the Chummer5a repo (default: {DEFAULT_CHUMMER5A_REF})")
    parser.add_argument("--fixture-inventory", type=Path, default=DEFAULT_FIXTURE_INVENTORY, help=f"Fixture inventory YAML (default: {DEFAULT_FIXTURE_INVENTORY})")
    parser.add_argument("--oracle-baselines", type=Path, default=DEFAULT_ORACLE_BASELINES, help=f"Oracle baseline YAML (default: {DEFAULT_ORACLE_BASELINES})")
    parser.add_argument("--workflow-pack", type=Path, default=DEFAULT_WORKFLOW_PACK, help=f"Veteran workflow pack YAML (default: {DEFAULT_WORKFLOW_PACK})")
    parser.add_argument("--screenshot-evidence", type=Path, default=DEFAULT_SCREENSHOT_EVIDENCE, help=f"Screenshot control evidence JSON (default: {DEFAULT_SCREENSHOT_EVIDENCE})")
    parser.add_argument("--workflow-parity-dir", type=Path, default=DEFAULT_WORKFLOW_PARITY_DIR, help=f"Published workflow-parity receipt directory (default: {DEFAULT_WORKFLOW_PARITY_DIR})")
    parser.add_argument("--executed-workflow-parity-dir", type=Path, default=DEFAULT_EXECUTED_WORKFLOW_PARITY_DIR, help=f"Published executed workflow receipt directory (default: {DEFAULT_EXECUTED_WORKFLOW_PARITY_DIR})")
    parser.add_argument("--visual-gate", type=Path, default=DEFAULT_VISUAL_GATE, help=f"Desktop visual familiarity gate receipt (default: {DEFAULT_VISUAL_GATE})")
    parser.add_argument("--flagship-gate", type=Path, default=DEFAULT_FLAGSHIP_GATE, help=f"UI flagship gate receipt (default: {DEFAULT_FLAGSHIP_GATE})")
    parser.add_argument("--executable-gate", type=Path, default=DEFAULT_EXECUTABLE_GATE, help=f"Desktop executable gate receipt (default: {DEFAULT_EXECUTABLE_GATE})")
    parser.add_argument("--user-journey-trace", type=Path, default=DEFAULT_USER_JOURNEY_TRACE, help=f"Published user-journey tester trace (default: {DEFAULT_USER_JOURNEY_TRACE})")
    parser.add_argument("--user-journey-audit", type=Path, default=DEFAULT_USER_JOURNEY_AUDIT, help=f"Published user-journey tester audit receipt (default: {DEFAULT_USER_JOURNEY_AUDIT})")
    parser.add_argument("--user-journey-screenshot-dir", type=Path, default=DEFAULT_USER_JOURNEY_SCREENSHOT_DIR, help=f"Directory containing published user-journey screenshots (default: {DEFAULT_USER_JOURNEY_SCREENSHOT_DIR})")
    parser.add_argument("--reconstruction-receipts-dir", type=Path, default=DEFAULT_RECONSTRUCTION_RECEIPTS_DIR, help=f"Directory containing per-fixture UI reconstruction receipts for the selected fixture set (default: {DEFAULT_RECONSTRUCTION_RECEIPTS_DIR})")
    parser.add_argument("--desktop-workflow-parity-receipt", type=Path, default=DEFAULT_DESKTOP_WORKFLOW_PARITY_RECEIPT, help=f"Published Chummer5a desktop workflow parity receipt (default: {DEFAULT_DESKTOP_WORKFLOW_PARITY_RECEIPT})")
    parser.add_argument("--generated-dialog-element-parity-receipt", type=Path, default=DEFAULT_GENERATED_DIALOG_ELEMENT_PARITY_RECEIPT, help=f"Published generated dialog element parity receipt (default: {DEFAULT_GENERATED_DIALOG_ELEMENT_PARITY_RECEIPT})")
    parser.add_argument("--ui-element-parity-audit-receipt", type=Path, default=DEFAULT_UI_ELEMENT_PARITY_AUDIT_RECEIPT, help=f"Published UI element parity audit receipt (default: {DEFAULT_UI_ELEMENT_PARITY_AUDIT_RECEIPT})")
    parser.add_argument("--artifacts", type=Path, default=DEFAULT_ARTIFACTS, help=f"Output artifacts directory (default: {DEFAULT_ARTIFACTS})")
    parser.add_argument("--resolution", default="1920x1080", help="Recorded execution resolution (default: 1920x1080)")
    parser.add_argument("--scale", default="1.0", help="Recorded UI scale factor (default: 1.0)")
    parser.add_argument("--fixture", action="append", help="Explicit .chum5 fixture path. Repeat to override the default first-slice fixture set.")
    return parser


def main(argv: list[str]) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        return run_gate(args)
    except (FileNotFoundError, RuntimeError, ValueError, ET.ParseError, subprocess.SubprocessError) as exc:
        print(f"tester infrastructure failure: {exc}", file=sys.stderr)
        return INFRA_EXIT


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
