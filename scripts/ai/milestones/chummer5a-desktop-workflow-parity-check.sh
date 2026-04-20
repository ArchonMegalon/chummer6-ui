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
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

receipt_path, ledger_path, oracle_path, checklist_path, dual_head_tests_path, compliance_tests_path, ui_gate_tests_path = [
    Path(value) for value in sys.argv[1:8]
]
release_channel_path = Path(sys.argv[8])


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
    "evidence": {},
}

if not ledger_path.is_file():
    payload["reasons"].append(f"Workflow parity ledger is missing: {ledger_path}")
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    raise SystemExit(43)

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

if not release_channel_path.is_file():
    payload["reasons"].append(f"Release channel receipt is missing: {release_channel_path}")
elif not isinstance(release_channel, dict) or not release_channel:
    payload["reasons"].append(
        f"Release channel receipt is unreadable or not a JSON object: {release_channel_path}"
    )
if not release_channel_channel_id:
    payload["reasons"].append("Release channel receipt is missing channelId/channel.")
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    payload["reasons"].append(
        "Release channel receipt is missing a valid generatedAt/generated_at timestamp."
    )

ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
oracle = json.loads(oracle_path.read_text(encoding="utf-8"))
checklist_text = checklist_path.read_text(encoding="utf-8") if checklist_path.is_file() else ""
test_corpus = "\n".join(
    path.read_text(encoding="utf-8")
    for path in [dual_head_tests_path, compliance_tests_path, ui_gate_tests_path]
    if path.is_file()
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
    payload["reasons"].append(f"Parity checklist still has {tabs_missing} tab entries missing in catalog coverage.")
if workspace_missing:
    payload["reasons"].append(f"Parity checklist still has {workspace_missing} workspace action entries missing in catalog coverage.")
if missing_family_ids:
    payload["reasons"].append("Workflow parity ledger is missing required families: " + ", ".join(missing_family_ids))
if non_ready_family_ids:
    payload["reasons"].append(
        "Workflow parity ledger has unresolved families: "
        + ", ".join(f"{family_id}={families[family_id].get('status', 'missing')}" for family_id in non_ready_family_ids)
    )
if missing_test_refs:
    payload["reasons"].append(
        "Workflow parity ledger references missing executable tests: "
        + ", ".join(f"{family_id}: {', '.join(names)}" for family_id, names in sorted(missing_test_refs.items()))
    )

if not payload["reasons"]:
    payload["status"] = "pass"
    payload["summary"] = "Chummer5a desktop workflow parity is explicitly proven across the promoted head."

payload["channelId"] = release_channel_channel_id
payload["evidence"] = {
    "releaseChannelPath": str(release_channel_path),
    "releaseChannelExists": release_channel_path.is_file(),
    "releaseChannelChannelId": release_channel_channel_id,
    "releaseChannelGeneratedAt": release_channel_generated_at_raw,
    "workflowLedgerPath": str(ledger_path),
    "parityOraclePath": str(oracle_path),
    "parityChecklistPath": str(checklist_path),
    "requiredFamilyCount": len(required_family_ids),
    "ledgerFamilyCount": len(families),
    "missingFamilyIds": missing_family_ids,
    "nonReadyFamilyIds": non_ready_family_ids,
    "tabsMissingInCatalog": tabs_missing,
    "workspaceActionsMissingInCatalog": workspace_missing,
    "missingTestRefs": missing_test_refs,
}

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if payload["status"] != "pass":
    raise SystemExit(43)
PY

echo "[workflow-parity] PASS"
