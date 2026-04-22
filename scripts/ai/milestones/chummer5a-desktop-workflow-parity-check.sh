#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json"
ledger_path="$repo_root/docs/WORKFLOW_PARITY_LEDGER.json"
oracle_path="$repo_root/docs/PARITY_ORACLE.json"
checklist_path="$repo_root/docs/PARITY_CHECKLIST.md"
dual_head_tests_path="$repo_root/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs"
compliance_tests_path="$repo_root/Chummer.Tests/Compliance/MigrationComplianceTests.cs"
ui_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
release_channel_path="${CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH:-$release_channel_path_default}"

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$receipt_path" "$ledger_path" "$oracle_path" "$checklist_path" "$dual_head_tests_path" "$compliance_tests_path" "$ui_gate_tests_path" "$release_channel_path"
from __future__ import annotations

import json
import os
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

receipt_path, ledger_path, oracle_path, checklist_path, dual_head_tests_path, compliance_tests_path, ui_gate_tests_path = [
    Path(value) for value in sys.argv[1:8]
]
release_channel_path = Path(sys.argv[8])
RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS") or "86400"
)
RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_EXECUTABLE_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)


def normalize(value: object) -> str:
    return str(value or "").strip().lower()


def parse_iso(value: object) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)

required_family_ids = [
    "create-open-import-save-save-as-print-export",
    "metatype-priorities-karma-entry",
    "attributes-skills-skill-groups-specializations-knowledge-languages",
    "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources",
    "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers",
    "cyberware-bioware-modular-hierarchies-nested-plugins",
    "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms",
    "improvements-explain-result-parity",
    "recovery-reload-migration-roundtrips",
    "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
]

payload = {
    "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    "contract_name": "chummer6-ui.chummer5a_desktop_workflow_parity",
    "channelId": "",
    "status": "fail",
    "summary": "Chummer5a desktop workflow parity is not yet exhaustively proven.",
    "reasons": [],
    "evidence": {
        "releaseChannelPath": str(release_channel_path),
        "releaseChannelExists": release_channel_path.is_file(),
        "workflowLedgerPath": str(ledger_path),
        "parityOraclePath": str(oracle_path),
        "parityChecklistPath": str(checklist_path),
        "dualHeadTestsPath": str(dual_head_tests_path),
        "complianceTestsPath": str(compliance_tests_path),
        "uiGateTestsPath": str(ui_gate_tests_path),
        "releaseChannelMaxAgeSeconds": RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS,
        "releaseChannelMaxFutureSkewSeconds": RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS,
    },
}

source_artifact_reasons: list[str] = []
release_channel_reasons: list[str] = []
checklist_coverage_reasons: list[str] = []
workflow_family_reasons: list[str] = []
test_reference_reasons: list[str] = []


def append_reason(message: str, *buckets: list[str]) -> None:
    if message not in payload["reasons"]:
        payload["reasons"].append(message)
    for bucket in buckets:
        if message not in bucket:
            bucket.append(message)


def require_file(path: Path, label: str) -> bool:
    if path.is_file():
        return True
    append_reason(f"{label} is missing: {path}", source_artifact_reasons)
    return False


ledger_exists = require_file(ledger_path, "Workflow parity ledger")
oracle_exists = require_file(oracle_path, "Parity oracle")
checklist_exists = require_file(checklist_path, "Parity checklist")
dual_head_tests_exist = require_file(dual_head_tests_path, "Dual-head acceptance tests")
compliance_tests_exist = require_file(compliance_tests_path, "Migration compliance tests")
ui_gate_tests_exist = require_file(ui_gate_tests_path, "Flagship UI gate tests")

release_channel = {}
if release_channel_path.is_file():
    loaded = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
    if isinstance(loaded, dict):
        release_channel = loaded
release_channel_channel_id = normalize(
    release_channel.get("channelId") if isinstance(release_channel, dict) else ""
)
if not release_channel_channel_id:
    release_channel_channel_id = normalize(
        release_channel.get("channel") if isinstance(release_channel, dict) else ""
    )
release_channel_generated_at_raw = ""
release_channel_generated_at = None
for key in ("generatedAt", "generated_at"):
    if isinstance(release_channel, dict) and key in release_channel:
        release_channel_generated_at_raw = str(release_channel.get(key) or "").strip()
        release_channel_generated_at = parse_iso(release_channel_generated_at_raw)
        break

release_channel_age_seconds = None
release_channel_future_skew_seconds = None
if not release_channel_path.is_file():
    append_reason(f"Release channel receipt is missing: {release_channel_path}", release_channel_reasons)
elif not isinstance(release_channel, dict) or not release_channel:
    append_reason(
        f"Release channel receipt is unreadable or not a JSON object: {release_channel_path}"
        , release_channel_reasons
    )
if not release_channel_channel_id:
    append_reason("Release channel receipt is missing channelId/channel.", release_channel_reasons)
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    append_reason(
        "Release channel receipt is missing a valid generatedAt/generated_at timestamp."
        , release_channel_reasons
    )
else:
    now = datetime.now(timezone.utc)
    release_channel_delta_seconds = (now - release_channel_generated_at).total_seconds()
    release_channel_age_seconds = int(max(release_channel_delta_seconds, 0))
    release_channel_future_skew_seconds = int(max(-release_channel_delta_seconds, 0))
    if release_channel_future_skew_seconds > RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS:
        append_reason(
            f"Release channel receipt generatedAt is in the future by {release_channel_future_skew_seconds} seconds.",
            release_channel_reasons,
        )
    if release_channel_age_seconds > RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS:
        append_reason(
            f"Release channel receipt is stale ({release_channel_age_seconds} seconds old).",
            release_channel_reasons,
        )

ledger = json.loads(ledger_path.read_text(encoding="utf-8")) if ledger_exists else {}
oracle = json.loads(oracle_path.read_text(encoding="utf-8")) if oracle_exists else {}
checklist_text = checklist_path.read_text(encoding="utf-8") if checklist_exists else ""
test_corpus = "\n".join(
    path.read_text(encoding="utf-8")
    for path, exists in [
        (dual_head_tests_path, dual_head_tests_exist),
        (compliance_tests_path, compliance_tests_exist),
        (ui_gate_tests_path, ui_gate_tests_exist),
    ]
    if exists
)

summary_match = re.search(
    r"\| Workspace Actions \| (?P<legacy>\d+) \| (?P<covered>\d+) \| (?P<missing>\d+) \| (?P<catalog_only>\d+) \|",
    checklist_text,
)
tabs_match = re.search(
    r"\| Tabs \| (?P<legacy>\d+) \| (?P<covered>\d+) \| (?P<missing>\d+) \| (?P<catalog_only>\d+) \|",
    checklist_text,
)

workspace_missing = int(summary_match.group("missing")) if summary_match else len(oracle.get("workspaceActions") or [])
tabs_missing = int(tabs_match.group("missing")) if tabs_match else len(oracle.get("tabs") or [])

families = {str(item.get("id") or "").strip(): item for item in (ledger.get("requiredFamilies") or []) if isinstance(item, dict)}
missing_family_ids = [family_id for family_id in required_family_ids if family_id not in families]
non_ready_family_ids = [
    family_id
    for family_id in required_family_ids
    if family_id in families and str(families[family_id].get("status") or "").strip().lower() != "ready"
]

missing_test_refs: dict[str, list[str]] = {}
for family_id in required_family_ids:
    family = families.get(family_id) or {}
    audit_tests = [str(value).strip() for value in (family.get("auditTests") or []) if str(value).strip()]
    unresolved = [name for name in audit_tests if name not in test_corpus]
    if unresolved:
        missing_test_refs[family_id] = unresolved

if tabs_missing:
    append_reason(
        f"Parity checklist still has {tabs_missing} tab entries missing in catalog coverage.",
        checklist_coverage_reasons,
    )
if workspace_missing:
    append_reason(
        f"Parity checklist still has {workspace_missing} workspace action entries missing in catalog coverage.",
        checklist_coverage_reasons,
    )
if missing_family_ids:
    append_reason(
        "Workflow parity ledger is missing required families: " + ", ".join(missing_family_ids),
        workflow_family_reasons,
    )
if non_ready_family_ids:
    append_reason(
        "Workflow parity ledger has unresolved families: "
        + ", ".join(f"{family_id}={families[family_id].get('status', 'missing')}" for family_id in non_ready_family_ids)
        , workflow_family_reasons
    )
if missing_test_refs:
    append_reason(
        "Workflow parity ledger references missing executable tests: "
        + ", ".join(f"{family_id}: {', '.join(names)}" for family_id, names in sorted(missing_test_refs.items()))
        , test_reference_reasons
    )

if not payload["reasons"]:
    payload["status"] = "pass"
    payload["summary"] = (
        "Chummer5a desktop workflow parity is explicitly proven across source artifacts, release-channel identity, "
        "catalog coverage, workflow-family readiness, and executable test references."
    )

payload["channelId"] = release_channel_channel_id
payload["evidence"]["releaseChannelChannelId"] = release_channel_channel_id
payload["evidence"]["releaseChannelGeneratedAt"] = release_channel_generated_at_raw
payload["evidence"]["releaseChannelAgeSeconds"] = release_channel_age_seconds
payload["evidence"]["releaseChannelFutureSkewSeconds"] = release_channel_future_skew_seconds
payload["evidence"]["requiredFamilyCount"] = len(required_family_ids)
payload["evidence"]["ledgerFamilyCount"] = len(families)
payload["evidence"]["missingFamilyIds"] = missing_family_ids
payload["evidence"]["nonReadyFamilyIds"] = non_ready_family_ids
payload["evidence"]["tabsMissingInCatalog"] = tabs_missing
payload["evidence"]["workspaceActionsMissingInCatalog"] = workspace_missing
payload["evidence"]["missingTestRefs"] = missing_test_refs
payload["evidence"]["sourceArtifactChecks"] = {
    "workflowLedger": ledger_exists,
    "parityOracle": oracle_exists,
    "parityChecklist": checklist_exists,
    "dualHeadTests": dual_head_tests_exist,
    "complianceTests": compliance_tests_exist,
    "uiGateTests": ui_gate_tests_exist,
}
payload["evidence"]["failureCount"] = len(payload["reasons"])

payload["sourceArtifactReview"] = {
    "status": "pass" if not source_artifact_reasons else "fail",
    "summary": (
        "Ledger, oracle, checklist, and executable test sources are present for Chummer5a workflow parity."
        if not source_artifact_reasons
        else "One or more Chummer5a workflow parity source artifacts are missing."
    ),
    "reasons": source_artifact_reasons,
    "checks": payload["evidence"]["sourceArtifactChecks"],
}
payload["releaseChannelReview"] = {
    "status": "pass" if not release_channel_reasons else "fail",
    "summary": (
        "Chummer5a workflow parity proof is aligned to a current release-channel identity."
        if not release_channel_reasons
        else "Chummer5a workflow parity proof is missing or drifting from release-channel identity."
    ),
    "reasons": release_channel_reasons,
    "path": str(release_channel_path),
    "channelId": release_channel_channel_id,
    "generatedAt": release_channel_generated_at_raw,
    "ageSeconds": release_channel_age_seconds,
    "futureSkewSeconds": release_channel_future_skew_seconds,
    "maxAgeSeconds": RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS,
    "maxFutureSkewSeconds": RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS,
}
payload["checklistCoverageReview"] = {
    "status": "pass" if not checklist_coverage_reasons else "fail",
    "summary": (
        "Parity checklist tab and workspace-action coverage is complete."
        if not checklist_coverage_reasons
        else "Parity checklist coverage still has missing tab or workspace-action entries."
    ),
    "reasons": checklist_coverage_reasons,
    "tabsMissingInCatalog": tabs_missing,
    "workspaceActionsMissingInCatalog": workspace_missing,
}
payload["workflowFamilyReview"] = {
    "status": "pass" if not workflow_family_reasons else "fail",
    "summary": (
        "All required Chummer5a workflow families are present and ready."
        if not workflow_family_reasons
        else "One or more required Chummer5a workflow families are missing or non-ready."
    ),
    "reasons": workflow_family_reasons,
    "requiredFamilyCount": len(required_family_ids),
    "ledgerFamilyCount": len(families),
    "missingFamilyIds": missing_family_ids,
    "nonReadyFamilyIds": non_ready_family_ids,
}
payload["testReferenceReview"] = {
    "status": "pass" if not test_reference_reasons else "fail",
    "summary": (
        "Workflow parity ledger audit tests resolve to executable test sources."
        if not test_reference_reasons
        else "Workflow parity ledger still references missing executable tests."
    ),
    "reasons": test_reference_reasons,
    "missingTestRefs": missing_test_refs,
}

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if payload["status"] != "pass":
    raise SystemExit(43)
PY

echo "[workflow-parity] PASS"
