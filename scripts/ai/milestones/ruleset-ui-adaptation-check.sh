#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_RULESET_UI_ADAPTATION_RECEIPT_PATH:-$repo_root/.codex-studio/published/RULESET_UI_ADAPTATION.generated.json}"
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

echo "[UI-RS] checking ruleset-specific workbench adaptation guardrails..."

require_contains() {
  local path="$1"
  local pattern="$2"
  local message="$3"
  if rg -q "$pattern" "$path"; then
    return 0
  fi
  echo "$message" >>"$tmp_reasons_file"
  return 1
}

tmp_reasons_file="$(mktemp)"
trap 'rm -f "$tmp_reasons_file"' EXIT

require_contains \
  "docs/RULESET_UI_DIRECTIVE.md" \
  'UI-RS-01|UI-RS-02|UI-RS-03|UI-RS-04|UI-RS-05|UI-RS-06|UI-RS-07|UI-RS-08' \
  "[UI-RS] FAIL: ruleset UI directive matrix is missing."

require_contains \
  "Chummer.Presentation/Rulesets/RulesetUiDirectiveCatalog.cs" \
  'private static readonly RulesetUiDirective Sr4|private static readonly RulesetUiDirective Sr5|private static readonly RulesetUiDirective Sr6' \
  "[UI-RS] FAIL: ruleset UI directive catalog does not define SR4/SR5/SR6 posture."

require_contains \
  "Chummer.Blazor/Components/Shell/WorkspaceLeftPane.razor" \
  'RulesetUiDirectiveCatalog.BuildNavigationTabsHeading|RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel|RulesetUiDirectiveCatalog.FormatWorkflowSurfaceLabel' \
  "[UI-RS] FAIL: Blazor workbench shell is not using the ruleset UI directive catalog."

require_contains \
  "Chummer.Avalonia/MainWindow.ShellFrameProjector.cs" \
  'RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading|RulesetUiDirectiveCatalog.BuildNavigationTabsHeading|RulesetUiDirectiveCatalog.FormatWorkflowSurfaceLabel' \
  "[UI-RS] FAIL: Avalonia workbench shell is not using the ruleset UI directive catalog."

require_contains \
  "Chummer.Tests/Presentation/RulesetUiDirectiveCatalogTests.cs" \
  'BuildComplianceRulesetSummary_distinguishes_sr4_sr5_and_sr6_posture|ShellDirectives_distinguish_headings_and_tab_action_labels_per_ruleset' \
  "[UI-RS] FAIL: ruleset directive unit coverage is missing."

require_contains \
  "Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs" \
  'DesktopShell_renders_ruleset_specific_flagship_posture_for_each_supported_lane|DesktopShell_uses_active_ruleset_plugin_catalogs_for_actions_and_workflow_surfaces' \
  "[UI-RS] FAIL: desktop-shell ruleset acceptance coverage is missing."

test_build_exit=0
if [[ ! -s "$tmp_reasons_file" ]]; then
  echo "[UI-RS] executing targeted ruleset posture and shell acceptance tests..."
  scripts/ai/with-package-plane.sh build Chummer.Tests/Chummer.Tests.csproj --nologo --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false || test_build_exit=$?
  scripts/ai/with-package-plane.sh build Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --nologo --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false || test_build_exit=$?
  scripts/ai/with-package-plane.sh run --project Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --no-build --nologo --verbosity quiet || test_build_exit=$?
fi

python3 - <<'PY' "$receipt_path" "$tmp_reasons_file" "$test_build_exit" "$release_channel_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

receipt_path_text, tmp_reasons_file_text, test_build_exit_text, release_channel_path_text = sys.argv[1:5]
receipt_path = Path(receipt_path_text)
tmp_reasons_file = Path(tmp_reasons_file_text)
test_build_exit = int(test_build_exit_text)
release_channel_path = Path(release_channel_path_text)

def normalize(value: object) -> str:
    return str(value or "").strip().lower()

def parse_iso(value: object):
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

payload = {
    "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    "contract_name": "chummer6-ui.ruleset_ui_adaptation_frontier",
    "channelId": "",
    "status": "fail",
    "summary": "Ruleset/UI adaptation proof is not yet fully established.",
    "reasons": [],
    "evidence": {
        "rulesetAdaptationReceiptPath": str(receipt_path),
        "releaseChannelPath": str(release_channel_path),
        "releaseChannelExists": release_channel_path.is_file(),
        "testBuildExit": test_build_exit,
        "requiredDirectives": [
            "UI-RS-01",
            "UI-RS-02",
            "UI-RS-03",
            "UI-RS-04",
            "UI-RS-05",
            "UI-RS-06",
            "UI-RS-07",
            "UI-RS-08",
        ],
    },
}

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
payload["channelId"] = release_channel_channel_id
payload["evidence"]["releaseChannelChannelId"] = release_channel_channel_id

if tmp_reasons_file.is_file():
    with tmp_reasons_file.open("r", encoding="utf-8") as handle:
        payload["reasons"].extend(
            [line.rstrip("\n") for line in handle.readlines() if line.strip()]
        )

if test_build_exit != 0:
    payload["reasons"].append(f"ruleset UI adaptation test/build execution exited non-zero: {test_build_exit}")

if not payload["reasons"]:
    payload["status"] = "pass"
    payload["summary"] = "Ruleset/UI adaptation parity is explicit and regression-guarded for Chummer desktop workbench workflows."

payload["evidence"]["failureCount"] = len(payload["reasons"])
payload["evidence"]["releaseChannelGeneratedAt"] = ""
if release_channel_path.is_file() and isinstance(release_channel, dict):
    payload["evidence"]["releaseChannelGeneratedAt"] = str(
        release_channel.get("generatedAt") or release_channel.get("generated_at") or ""
    ).strip()
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if payload["status"] != "pass":
    raise SystemExit(43)

print(f"[UI-RS] PASS: ruleset-specific workbench adaptation is explicit and regression-guarded.")
print(f"[UI-RS] evidence: {receipt_path}")
PY
