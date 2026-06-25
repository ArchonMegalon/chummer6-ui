#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"
workspace_root="$(cd "$repo_root/.." && pwd)"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-$workspace_root/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-$workspace_root/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-$workspace_root/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M143_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json}"

mkdir -p "$(dirname "$receipt_path")"

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$repo_root" <<'PY'
from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

registry_path = Path(sys.argv[1])
queue_path = Path(sys.argv[2])
design_queue_path = Path(sys.argv[3])
receipt_path = Path(sys.argv[4])
repo_root = Path(sys.argv[5])
canonical_ui_root = Path(os.environ.get("CHUMMER_NEXT90_M143_CANONICAL_UI_ROOT", str(repo_root)))
known_ui_root_aliases = {str(canonical_ui_root)}

if "CHUMMER_NEXT90_M143_CANONICAL_UI_ROOT" not in os.environ:
    sibling_alias = canonical_ui_root.with_name(
        "chummer-presentation" if canonical_ui_root.name == "chummer6-ui" else "chummer6-ui"
    )
    if sibling_alias.is_dir():
        known_ui_root_aliases.add(str(sibling_alias))

skip_flagship_gate_dependency = os.environ.get("CHUMMER_NEXT90_M143_SKIP_FLAGSHIP_GATE_DEPENDENCY", "").strip() == "1"

PACKAGE_ID = "next90-m143-ui-capture-direct-screenshot-and-runtime-proof-for-print-export-exchange-sr6"
TITLE = "Capture direct screenshot and runtime proof for print, export, exchange, SR6 supplement, and house-rule workflows."
TASK = TITLE
FRONTIER_ID = 6764868619
MILESTONE_ID = 143
WORK_TASK_ID = "143.1"
WAVE = "W22P"
EXPECTED_STATUS = "complete"
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = (
    "M143 chummer6-ui print/export/exchange and SR6 supplement/house-rule direct proof is complete; "
    "future shards must verify the closed-package receipt, focused guard test, route-local output gates, canonical registry row, "
    "and queue mirrors instead of reopening this slice."
)
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "capture_direct_screenshot_and_runtime_proof_for_print_ex:ui",
]
EXPECTED_DIRECT_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m143-ui-direct-output-proof-check.sh"
EXPECTED_TARGETED_TEST_COMMAND = (
    'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M143DirectOutputProofGuardTests" --no-restore'
)
EXPECTED_PROOF = [
    f"{canonical_ui_root}/Chummer.Tests/Compliance/Next90M143DirectOutputProofGuardTests.cs",
    f"{canonical_ui_root}/Chummer.Tests/Chummer.Tests.csproj",
    f"{canonical_ui_root}/scripts/ai/milestones/next90-m143-ui-direct-output-proof-check.sh",
    f"{canonical_ui_root}/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh",
    f"{canonical_ui_root}/scripts/ai/milestones/section-host-ruleset-parity-check.sh",
    f"{canonical_ui_root}/scripts/ai/milestones/generated-dialog-element-parity-check.sh",
    f"{canonical_ui_root}/scripts/ai/milestones/next90-m114-ui-rule-studio-check.sh",
    f"{canonical_ui_root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh",
    f"{canonical_ui_root}/scripts/ai/verify.sh",
    f"{canonical_ui_root}/.codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json",
    f"{canonical_ui_root}/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json",
    f"{canonical_ui_root}/.codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json",
    f"{canonical_ui_root}/.codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json",
    f"{canonical_ui_root}/.codex-studio/published/NEXT90_M114_UI_RULE_STUDIO.generated.json",
    f"{canonical_ui_root}/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json",
    f"{canonical_ui_root}/.codex-studio/published/NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json",
]
EXPECTED_REGISTRY_EVIDENCE = [
    (
        f"{canonical_ui_root}/scripts/ai/milestones/chummer5a-screenshot-review-gate.sh, "
        f"{canonical_ui_root}/scripts/ai/milestones/section-host-ruleset-parity-check.sh, "
        f"{canonical_ui_root}/scripts/ai/milestones/generated-dialog-element-parity-check.sh, and "
        f"{canonical_ui_root}/scripts/ai/milestones/b14-flagship-ui-release-gate.sh keep print/export/exchange plus SR6 supplement/house-rule proof "
        "bound to direct screenshot-backed and runtime-backed route receipts instead of family prose."
    ),
    (
        f"{canonical_ui_root}/.codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json, "
        f"{canonical_ui_root}/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json, "
        f"{canonical_ui_root}/.codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json, "
        f"{canonical_ui_root}/.codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json, "
        f"{canonical_ui_root}/.codex-studio/published/NEXT90_M114_UI_RULE_STUDIO.generated.json, "
        f"{canonical_ui_root}/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json, and "
        f"{canonical_ui_root}/.codex-studio/published/NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json keep the milestone-143 parity families aligned to route-local output proof."
    ),
    (
        f"{canonical_ui_root}/Chummer.Tests/Compliance/Next90M143DirectOutputProofGuardTests.cs, "
        f"{canonical_ui_root}/Chummer.Tests/Chummer.Tests.csproj, "
        f"{canonical_ui_root}/scripts/ai/milestones/next90-m143-ui-direct-output-proof-check.sh, and "
        f"{canonical_ui_root}/scripts/ai/verify.sh fail closed when canonical registry rows, queue mirrors, route-local receipts, or verify wiring drift from the completed package contract."
    ),
    (
        f"{canonical_ui_root}/.codex-studio/published/NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json records the closed-package receipt for "
        f"`{PACKAGE_ID}`."
    ),
]
EXPECTED_PARITY_ROW_IDS = [
    "family:sheet_export_print_viewer_and_exchange",
    "family:sr6_supplements_designers_and_house_rules",
]
EXPECTED_ROUTE_RECEIPTS = {
    "print_export_exchange": {
        "routeIds": [
            "screenshot:print_export_exchange",
            "print_export_exchange",
            "open_for_printing_menu_route",
            "open_for_export_menu_route",
            "print_multiple_menu_route",
        ],
        "workflowFamilyId": "create-open-import-save-save-as-print-export",
        "screenshots": [
            "19-workflow-file-menu-loaded-light.png",
            "18-import-dialog-light.png",
        ],
    },
    "sr6_supplements_and_house_rules": {
        "routeIds": [
            "screenshot:sr6_supplements_and_house_rules",
            "sr6_rule_environment",
            "sr6_supplements",
            "house_rules",
        ],
        "workflowFamilyId": "improvements-explain-result-parity",
        "screenshots": [
            "34-workflow-validate-section-light.png",
            "35-workflow-rules-section-light.png",
        ],
    },
}
EXPECTED_SCREENSHOTS = sorted(
    {
        "18-import-dialog-light.png",
        "19-workflow-file-menu-loaded-light.png",
        "34-workflow-validate-section-light.png",
        "35-workflow-rules-section-light.png",
    }
)
SOURCE_MARKERS = {
    "Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs": [
        'Command("open_for_printing", "command.open_for_printing", "file", false)',
        'Command("open_for_export", "command.open_for_export", "file", false)',
        'Command("print_setup", "command.print_setup", "file", false)',
        'Command("print_multiple", "command.print_multiple", "file", false)',
    ],
    "Chummer.Presentation/Overview/DesktopDialogFactory.cs": [
        '"open_for_printing" => CreateOpenCharacterDialog(',
        '"dialog.open_for_printing"',
        '"open_for_export" => CreateOpenCharacterDialog(',
        '"dialog.open_for_export"',
        '"print_setup" => new DesktopDialogState(',
        '"dialog.print_setup"',
        '"print_multiple" => new DesktopDialogState(',
        '"dialog.print_multiple"',
    ],
    "Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs": [
        '"open_for_printing"',
        '"open_for_export"',
        '"print_setup"',
        '"print_multiple"',
    ],
    "Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs": [
        '"print_setup"',
        '"print_multiple"',
    ],
    "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs": [
        "Runtime_backed_file_menu_restores_classic_save_and_print_commands",
        "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
        '"18-import-dialog-light.png"',
        '"19-workflow-file-menu-loaded-light.png"',
        '"34-workflow-validate-section-light.png"',
        '"35-workflow-rules-section-light.png"',
    ],
    "scripts/ai/milestones/chummer5a-screenshot-review-gate.sh": [
        '"print_export_exchange"',
        '"sr6_supplements_and_house_rules"',
        '"screenshot:print_export_exchange"',
        '"open_for_printing_menu_route"',
        '"open_for_export_menu_route"',
        '"print_multiple_menu_route"',
        '"screenshot:sr6_supplements_and_house_rules"',
        '"sr6_supplements"',
        '"house_rules"',
        '"19-workflow-file-menu-loaded-light.png"',
        '"18-import-dialog-light.png"',
        '"34-workflow-validate-section-light.png"',
        '"35-workflow-rules-section-light.png"',
    ],
    "scripts/ai/milestones/section-host-ruleset-parity-check.sh": [
        '"open_for_printing"',
        '"open_for_export"',
        '"print_setup"',
        '"print_multiple"',
    ],
    "scripts/ai/milestones/generated-dialog-element-parity-check.sh": [
        'GENERATED_DIALOG_ELEMENT_PARITY.generated.json',
    ],
    "scripts/ai/milestones/next90-m114-ui-rule-studio-check.sh": [
        'NEXT90_M114_UI_RULE_STUDIO.generated.json',
        'EXPECTED_TARGETED_TEST_COMMAND = \'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~Next90M114RuleEnvironmentStudioGuardTests" --no-restore\'',
    ],
    "scripts/ai/milestones/b14-flagship-ui-release-gate.sh": [
        '"18-import-dialog-light.png"',
        '"19-workflow-file-menu-loaded-light.png"',
        '"34-workflow-validate-section-light.png"',
        '"35-workflow-rules-section-light.png"',
    ],
    "scripts/ai/verify.sh": [
        "checking next-90 M143 direct output proof guard",
        "bash scripts/ai/milestones/next90-m143-ui-direct-output-proof-check.sh",
    ],
}
DISALLOWED_PROOF_TOKENS = [
    "TASK_LOCAL_TELEMETRY.generated.json",
    "ACTIVE_RUN_HANDOFF.generated.md",
    "supervisor status",
    "operator telemetry",
]


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(read_text(path))


def normalize(value: Any) -> str:
    return str(value or "").strip().lower()


def normalize_inline_whitespace(value: str) -> str:
    return " ".join(str(value or "").split())


def normalize_ui_paths_for_compare(value: str) -> str:
    normalized = normalize_inline_whitespace(value)
    for alias in sorted(known_ui_root_aliases, key=len, reverse=True):
        normalized = normalized.replace(normalize_inline_whitespace(alias), "<UI_REPO_ROOT>")
    return normalized


def extract_block(text: str, anchor: str, next_anchors: list[str]) -> str:
    start = text.find(anchor)
    if start < 0:
        return ""
    end_candidates = [text.find(candidate, start + len(anchor)) for candidate in next_anchors]
    end_candidates = [index for index in end_candidates if index >= 0]
    end = min(end_candidates) if end_candidates else len(text)
    return text[start:end]


registry_text = read_text(registry_path) if registry_path.is_file() else ""
queue_text = read_text(queue_path) if queue_path.is_file() else ""
design_queue_text = read_text(design_queue_path) if design_queue_path.is_file() else ""

registry_block = extract_block(registry_text, "- id: '143.1'", ["- id: '143.2'"])
queue_block = extract_block(queue_text, f"package_id: {PACKAGE_ID}", ["- title: "])
design_queue_block = extract_block(design_queue_text, f"package_id: {PACKAGE_ID}", ["- title: "])
registry_block_normalized = normalize_inline_whitespace(registry_block)
queue_block_normalized = normalize_inline_whitespace(queue_block)
design_queue_block_normalized = normalize_inline_whitespace(design_queue_block)

parity_audit = read_json(repo_root / ".codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json")
screenshot_review = read_json(repo_root / ".codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json")
section_host_parity = read_json(repo_root / ".codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json")
generated_dialog_parity = read_json(repo_root / ".codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json")
rule_studio = read_json(repo_root / ".codex-studio/published/NEXT90_M114_UI_RULE_STUDIO.generated.json")
ui_flagship_gate = read_json(repo_root / ".codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json")

payload: dict[str, Any] = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.next90_m143_ui_direct_output_proof",
    "status": "fail",
    "summary": "Milestone 143 direct output proof is incomplete.",
    "unresolved": [],
    "evidence": {
        "packageId": PACKAGE_ID,
        "frontierId": FRONTIER_ID,
        "milestoneId": MILESTONE_ID,
        "workTaskId": WORK_TASK_ID,
        "wave": WAVE,
        "repo": "chummer6-ui",
        "allowedPaths": EXPECTED_ALLOWED_PATHS,
        "ownedSurfaces": EXPECTED_SURFACES,
        "queueChecks": {},
        "parityAuditChecks": {},
        "receiptChecks": {},
        "routeReceiptChecks": {},
        "sourceChecks": {},
        "screenshotFiles": {},
        "proofFiles": EXPECTED_PROOF,
        "proofCommands": [
            EXPECTED_DIRECT_PROOF_COMMAND,
            EXPECTED_TARGETED_TEST_COMMAND,
        ],
        "supportingReceipts": {
            "parityAudit": str(repo_root / ".codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"),
            "screenshotReview": str(repo_root / ".codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"),
            "sectionHostParity": str(repo_root / ".codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json"),
            "generatedDialogParity": str(repo_root / ".codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json"),
            "ruleStudio": str(repo_root / ".codex-studio/published/NEXT90_M114_UI_RULE_STUDIO.generated.json"),
            "uiFlagshipGate": str(repo_root / ".codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"),
            "receipt": str(receipt_path),
        },
    },
}
unresolved: list[str] = payload["unresolved"]


def add_failure(message: str) -> None:
    if message not in unresolved:
        unresolved.append(message)


queue_checks: dict[str, bool] = {
    "registry_block_present": bool(registry_block),
    "queue_block_present": bool(queue_block),
    "design_queue_block_present": bool(design_queue_block),
}
queue_checks["registry_status_complete"] = f"status: {EXPECTED_STATUS}" in registry_block
queue_checks["queue_status_complete"] = f"status: {EXPECTED_STATUS}" in queue_block
queue_checks["design_queue_status_complete"] = f"status: {EXPECTED_STATUS}" in design_queue_block
queue_checks["registry_completion_action_matches"] = f"completion_action: {EXPECTED_COMPLETION_ACTION}" in registry_block
queue_checks["queue_completion_action_matches"] = f"completion_action: {EXPECTED_COMPLETION_ACTION}" in queue_block
queue_checks["design_queue_completion_action_matches"] = f"completion_action: {EXPECTED_COMPLETION_ACTION}" in design_queue_block
queue_checks["registry_do_not_reopen_reason_matches"] = normalize_inline_whitespace(EXPECTED_DO_NOT_REOPEN_REASON) in registry_block_normalized
queue_checks["queue_do_not_reopen_reason_matches"] = normalize_inline_whitespace(EXPECTED_DO_NOT_REOPEN_REASON) in queue_block_normalized
queue_checks["design_queue_do_not_reopen_reason_matches"] = normalize_inline_whitespace(EXPECTED_DO_NOT_REOPEN_REASON) in design_queue_block_normalized
queue_checks["registry_title_matches"] = TITLE in registry_block
queue_checks["queue_frontier_matches"] = f"frontier_id: {FRONTIER_ID}" in queue_block
queue_checks["design_queue_frontier_matches"] = f"frontier_id: {FRONTIER_ID}" in design_queue_block
queue_checks["queue_work_task_matches"] = f"work_task_id: '{WORK_TASK_ID}'" in queue_block
queue_checks["design_queue_work_task_matches"] = f"work_task_id: '{WORK_TASK_ID}'" in design_queue_block
queue_checks["queue_wave_matches"] = f"wave: {WAVE}" in queue_block
queue_checks["design_queue_wave_matches"] = f"wave: {WAVE}" in design_queue_block
queue_checks["queue_repo_matches"] = "repo: chummer6-ui" in queue_block
queue_checks["design_queue_repo_matches"] = "repo: chummer6-ui" in design_queue_block
allowed_paths_block = "\n".join(f"  - {value}" for value in EXPECTED_ALLOWED_PATHS)
owned_surfaces_block = "\n".join(f"  - {value}" for value in EXPECTED_SURFACES)
queue_checks["queue_allowed_paths_exact"] = allowed_paths_block in queue_block
queue_checks["design_queue_allowed_paths_exact"] = allowed_paths_block in design_queue_block
queue_checks["queue_owned_surfaces_exact"] = owned_surfaces_block in queue_block
queue_checks["design_queue_owned_surfaces_exact"] = owned_surfaces_block in design_queue_block
queue_checks["registry_evidence_exact"] = all(normalize_inline_whitespace(line) in registry_block_normalized for line in EXPECTED_REGISTRY_EVIDENCE)
queue_checks["registry_evidence_exact"] = all(
    normalize_ui_paths_for_compare(line) in normalize_ui_paths_for_compare(registry_block)
    for line in EXPECTED_REGISTRY_EVIDENCE
)
queue_checks["queue_proof_exact"] = all(
    normalize_ui_paths_for_compare(line) in normalize_ui_paths_for_compare(queue_block)
    for line in EXPECTED_PROOF + [EXPECTED_DIRECT_PROOF_COMMAND, EXPECTED_TARGETED_TEST_COMMAND]
)
queue_checks["design_queue_proof_exact"] = all(
    normalize_ui_paths_for_compare(line) in normalize_ui_paths_for_compare(design_queue_block)
    for line in EXPECTED_PROOF + [EXPECTED_DIRECT_PROOF_COMMAND, EXPECTED_TARGETED_TEST_COMMAND]
)
queue_checks["queue_design_block_parity"] = queue_block_normalized == design_queue_block_normalized and bool(queue_block)
queue_checks["registry_worker_safe"] = not any(token.lower() in registry_block.lower() for token in DISALLOWED_PROOF_TOKENS)
queue_checks["queue_worker_safe"] = not any(token.lower() in queue_block.lower() for token in DISALLOWED_PROOF_TOKENS)
queue_checks["design_queue_worker_safe"] = not any(token.lower() in design_queue_block.lower() for token in DISALLOWED_PROOF_TOKENS)
payload["evidence"]["queueChecks"] = queue_checks

for name, passed in queue_checks.items():
    if not passed:
        add_failure(f"queue/registry proof check failed: {name}")

rows = {row.get("id"): row for row in parity_audit.get("rows", []) if isinstance(row, dict)}
parity_checks: dict[str, bool] = {
    "parity_audit_status_pass": normalize(parity_audit.get("status")) == "pass",
}
for row_id in EXPECTED_PARITY_ROW_IDS:
    row = rows.get(row_id, {})
    key_prefix = row_id.replace("family:", "")
    parity_checks[f"{key_prefix}_row_present"] = bool(row)
    parity_checks[f"{key_prefix}_visual_yes"] = normalize(row.get("visual_parity")) == "yes"
    parity_checks[f"{key_prefix}_behavioral_yes"] = normalize(row.get("behavioral_parity")) == "yes"
    evidence_list = row.get("evidence") if isinstance(row.get("evidence"), list) else []
    if row_id == "family:sheet_export_print_viewer_and_exchange":
        required_evidence = [
            str(canonical_ui_root / ".codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json"),
            str(canonical_ui_root / ".codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json"),
            str(canonical_ui_root / ".codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"),
        ]
    else:
        required_evidence = [
            str(canonical_ui_root / ".codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"),
            str(canonical_ui_root / ".codex-studio/published/NEXT90_M114_UI_RULE_STUDIO.generated.json"),
        ]
    normalized_evidence = {
        normalize_ui_paths_for_compare(item)
        for item in evidence_list
        if isinstance(item, str)
    }
    parity_checks[f"{key_prefix}_evidence_present"] = all(
        normalize_ui_paths_for_compare(required_item) in normalized_evidence
        for required_item in required_evidence
    )
payload["evidence"]["parityAuditChecks"] = parity_checks

for name, passed in parity_checks.items():
    if not passed:
        add_failure(f"parity audit check failed: {name}")

receipt_checks: dict[str, bool] = {
    "screenshot_review_status_pass": normalize(screenshot_review.get("status")) == "pass",
    "section_host_status_pass": normalize(section_host_parity.get("status")) == "pass",
    "generated_dialog_status_pass": normalize(generated_dialog_parity.get("status")) == "pass",
    "rule_studio_status_pass": normalize(rule_studio.get("status")) == "pass",
    "ui_flagship_gate_status_pass": normalize(ui_flagship_gate.get("status")) == "pass"
    or skip_flagship_gate_dependency,
}

section_command_ids = (
    ((section_host_parity.get("evidence") or {}).get("expectedCommandIds"))
    if isinstance((section_host_parity.get("evidence") or {}).get("expectedCommandIds"), list)
    else []
)
for command_id in ["open_for_printing", "open_for_export", "print_setup", "print_multiple"]:
    receipt_checks[f"section_host_{command_id}_present"] = command_id in section_command_ids

generated_dialog_text = json.dumps(generated_dialog_parity)
for command_id in ["open_for_printing", "open_for_export", "print_setup", "print_multiple"]:
    receipt_checks[f"generated_dialog_{command_id}_present"] = command_id in generated_dialog_text

ui_flagship_gate_text = json.dumps(ui_flagship_gate)
for screenshot in EXPECTED_SCREENSHOTS:
    receipt_checks[f"ui_flagship_{screenshot}_present"] = skip_flagship_gate_dependency or screenshot in ui_flagship_gate_text

payload["evidence"]["receiptChecks"] = receipt_checks
for name, passed in receipt_checks.items():
    if not passed:
        add_failure(f"receipt check failed: {name}")

route_receipts = (
    ((screenshot_review.get("evidence") or {}).get("routeLocalReceipts"))
    if isinstance((screenshot_review.get("evidence") or {}).get("routeLocalReceipts"), dict)
    else {}
)
route_checks: dict[str, dict[str, bool]] = {}
for route_id, expected in EXPECTED_ROUTE_RECEIPTS.items():
    route = route_receipts.get(route_id, {})
    checks = {
        "exists": bool(route),
        "status_pass": normalize(route.get("status")) == "pass",
        "route_ids_exact": route.get("routeIds") == expected["routeIds"],
        "workflow_family_matches": route.get("workflowFamilyId") == expected["workflowFamilyId"],
        "screenshots_exact": route.get("screenshots") == expected["screenshots"],
    }
    route_checks[route_id] = checks
    for name, passed in checks.items():
        if not passed:
            add_failure(f"route receipt check failed: {route_id}.{name}")
payload["evidence"]["routeReceiptChecks"] = route_checks

reviewed_jobs = set((screenshot_review.get("evidence") or {}).get("reviewedJobs") or [])
failing_jobs = set((screenshot_review.get("evidence") or {}).get("failingJobs") or [])
receipt_checks["reviewed_jobs_are_known"] = all(
    name in reviewed_jobs for name in ["print_export_exchange", "sr6_supplements_and_house_rules", "translator", "xml_editor", "hero_lab_importer"]
)
receipt_checks["failing_jobs_clear"] = not failing_jobs
receipt_checks["route_local_receipts_present"] = all(name in route_receipts for name in EXPECTED_ROUTE_RECEIPTS)
if not receipt_checks["reviewed_jobs_are_known"]:
    add_failure("receipt check failed: reviewed_jobs_are_known")
if not receipt_checks["failing_jobs_clear"]:
    add_failure("receipt check failed: failing_jobs_clear")
if not receipt_checks["route_local_receipts_present"]:
    add_failure("receipt check failed: route_local_receipts_present")

source_checks: dict[str, dict[str, bool]] = {}
for relative_path, markers in SOURCE_MARKERS.items():
    path = repo_root / relative_path
    text = read_text(path) if path.is_file() else ""
    source_checks[relative_path] = {marker: marker in text for marker in markers}
    for marker, passed in source_checks[relative_path].items():
        if not passed:
            add_failure(f"source marker missing: {relative_path} -> {marker}")
payload["evidence"]["sourceChecks"] = source_checks

screenshot_files = {
    screenshot: screenshot in json.dumps(screenshot_review) or screenshot in ui_flagship_gate_text
    for screenshot in EXPECTED_SCREENSHOTS
}
payload["evidence"]["screenshotFiles"] = screenshot_files
for screenshot, present in screenshot_files.items():
    if not present:
        add_failure(f"screenshot proof missing: {screenshot}")

for proof_path in EXPECTED_PROOF:
    if proof_path.startswith(str(repo_root)):
        if Path(proof_path) == receipt_path:
            continue
        if not Path(proof_path).exists():
            add_failure(f"proof file missing on disk: {proof_path}")

if not unresolved:
    payload["status"] = "pass"
    payload["summary"] = "Milestone 143 direct output proof is closed on route-local screenshot, runtime, and queue/registry evidence."

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if unresolved:
    for entry in unresolved:
        print(f"[M143] FAIL: {entry}", file=sys.stderr)
    raise SystemExit(1)

print(f"[M143] PASS: {PACKAGE_ID} registry, queue, and direct output proof are closed.")
PY
