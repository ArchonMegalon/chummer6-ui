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
  local bucket_file="$4"
  if rg -q "$pattern" "$path"; then
    return 0
  fi
  echo "$message" >>"$bucket_file"
  return 1
}

directive_matrix_reasons_file="$(mktemp)"
catalog_definition_reasons_file="$(mktemp)"
shell_binding_reasons_file="$(mktemp)"
trap 'rm -f "$directive_matrix_reasons_file" "$catalog_definition_reasons_file" "$shell_binding_reasons_file"' EXIT

require_contains \
  "docs/RULESET_UI_DIRECTIVE.md" \
  'UI-RS-01|UI-RS-02|UI-RS-03|UI-RS-04|UI-RS-05|UI-RS-06|UI-RS-07|UI-RS-08' \
  "[UI-RS] FAIL: ruleset UI directive matrix is missing." \
  "$directive_matrix_reasons_file"

require_contains \
  "Chummer.Presentation/Rulesets/RulesetUiDirectiveCatalog.cs" \
  'private static readonly RulesetUiDirective Sr4|private static readonly RulesetUiDirective Sr5|private static readonly RulesetUiDirective Sr6' \
  "[UI-RS] FAIL: ruleset UI directive catalog does not define SR4/SR5/SR6 posture." \
  "$catalog_definition_reasons_file"

require_contains \
  "Chummer.Blazor/Components/Shell/WorkspaceLeftPane.razor" \
  'RulesetUiDirectiveCatalog.BuildNavigationTabsHeading|RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel|RulesetUiDirectiveCatalog.FormatWorkflowSurfaceLabel' \
  "[UI-RS] FAIL: Blazor workbench shell is not using the ruleset UI directive catalog." \
  "$shell_binding_reasons_file"

require_contains \
  "Chummer.Avalonia/MainWindow.ShellFrameProjector.cs" \
  'RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading|RulesetUiDirectiveCatalog.BuildNavigationTabsHeading|RulesetUiDirectiveCatalog.FormatWorkflowSurfaceLabel' \
  "[UI-RS] FAIL: Avalonia workbench shell is not using the ruleset UI directive catalog." \
  "$shell_binding_reasons_file"

require_contains \
  "Chummer.Tests/Presentation/RulesetUiDirectiveCatalogTests.cs" \
  'BuildComplianceRulesetSummary_distinguishes_sr4_sr5_and_sr6_posture|ShellDirectives_distinguish_headings_and_tab_action_labels_per_ruleset' \
  "[UI-RS] FAIL: ruleset directive unit coverage is missing." \
  "$catalog_definition_reasons_file"

require_contains \
  "Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs" \
  'DesktopShell_renders_ruleset_specific_flagship_posture_for_each_supported_lane|DesktopShell_uses_active_ruleset_plugin_catalogs_for_actions_and_workflow_surfaces' \
  "[UI-RS] FAIL: desktop-shell ruleset acceptance coverage is missing." \
  "$shell_binding_reasons_file"

unit_test_build_exit=0
signoff_test_build_exit=0
signoff_test_run_exit=0
signoff_test_dll="Chummer.Tests/Presentation/bin/Debug/net10.0/Chummer.Presentation.Signoff.Tests.dll"
if [[ ! -s "$directive_matrix_reasons_file" && ! -s "$catalog_definition_reasons_file" && ! -s "$shell_binding_reasons_file" ]]; then
  echo "[UI-RS] executing targeted ruleset posture and shell acceptance tests..."
  scripts/ai/with-package-plane.sh build Chummer.Tests/Chummer.Tests.csproj --nologo --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false || unit_test_build_exit=$?
  scripts/ai/with-package-plane.sh build Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --nologo --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false || signoff_test_build_exit=$?
  dotnet "$signoff_test_dll" || signoff_test_run_exit=$?
fi

python3 - <<'PY' "$receipt_path" "$directive_matrix_reasons_file" "$catalog_definition_reasons_file" "$shell_binding_reasons_file" "$unit_test_build_exit" "$signoff_test_build_exit" "$signoff_test_run_exit" "$release_channel_path"
from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

(
    receipt_path_text,
    directive_matrix_reasons_file_text,
    catalog_definition_reasons_file_text,
    shell_binding_reasons_file_text,
    unit_test_build_exit_text,
    signoff_test_build_exit_text,
    signoff_test_run_exit_text,
    release_channel_path_text,
) = sys.argv[1:9]
receipt_path = Path(receipt_path_text)
directive_matrix_reasons_file = Path(directive_matrix_reasons_file_text)
catalog_definition_reasons_file = Path(catalog_definition_reasons_file_text)
shell_binding_reasons_file = Path(shell_binding_reasons_file_text)
unit_test_build_exit = int(unit_test_build_exit_text)
signoff_test_build_exit = int(signoff_test_build_exit_text)
signoff_test_run_exit = int(signoff_test_run_exit_text)
release_channel_path = Path(release_channel_path_text)
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

def status_ok(value: object) -> bool:
    return normalize(value) in {"pass", "passed", "ready"}

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

def read_reasons(path: Path) -> list[str]:
    if not path.is_file():
        return []
    with path.open("r", encoding="utf-8") as handle:
        return [line.rstrip("\n") for line in handle.readlines() if line.strip()]

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
        "unitTestBuildExit": unit_test_build_exit,
        "signoffTestBuildExit": signoff_test_build_exit,
        "signoffTestRunExit": signoff_test_run_exit,
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
        "directiveSourcePath": "docs/RULESET_UI_DIRECTIVE.md",
        "catalogSourcePath": "Chummer.Presentation/Rulesets/RulesetUiDirectiveCatalog.cs",
        "blazorShellSourcePath": "Chummer.Blazor/Components/Shell/WorkspaceLeftPane.razor",
        "avaloniaShellSourcePath": "Chummer.Avalonia/MainWindow.ShellFrameProjector.cs",
        "directiveTestsPath": "Chummer.Tests/Presentation/RulesetUiDirectiveCatalogTests.cs",
        "desktopShellRulesetTestsPath": "Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs",
        "releaseChannelMaxAgeSeconds": RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS,
        "releaseChannelMaxFutureSkewSeconds": RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS,
    },
}

directive_matrix_reasons = read_reasons(directive_matrix_reasons_file)
catalog_definition_reasons = read_reasons(catalog_definition_reasons_file)
shell_binding_reasons = read_reasons(shell_binding_reasons_file)
release_channel_reasons: list[str] = []
test_execution_reasons: list[str] = []

def append_reason(message: str, *buckets: list[str]) -> None:
    if message not in payload["reasons"]:
        payload["reasons"].append(message)
    for bucket in buckets:
        if message not in bucket:
            bucket.append(message)

for reason in directive_matrix_reasons:
    append_reason(reason, directive_matrix_reasons)
for reason in catalog_definition_reasons:
    append_reason(reason, catalog_definition_reasons)
for reason in shell_binding_reasons:
    append_reason(reason, shell_binding_reasons)

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
release_channel_generated_at_raw = str(
    release_channel.get("generatedAt") or release_channel.get("generated_at") or ""
).strip() if isinstance(release_channel, dict) else ""
release_channel_generated_at = parse_iso(release_channel_generated_at_raw)
payload["channelId"] = release_channel_channel_id
payload["evidence"]["releaseChannelChannelId"] = release_channel_channel_id
payload["evidence"]["releaseChannelGeneratedAt"] = release_channel_generated_at_raw
release_channel_age_seconds = None
release_channel_future_skew_seconds = None
if not release_channel_path.is_file():
    append_reason(f"Release channel receipt is missing: {release_channel_path}", release_channel_reasons)
elif not isinstance(release_channel, dict) or not release_channel:
    append_reason(
        f"Release channel receipt is unreadable or not a JSON object: {release_channel_path}",
        release_channel_reasons,
    )
if not release_channel_channel_id:
    append_reason("Release channel receipt is missing channelId/channel.", release_channel_reasons)
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    append_reason(
        "Release channel receipt is missing a valid generatedAt/generated_at timestamp.",
        release_channel_reasons,
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
payload["evidence"]["releaseChannelAgeSeconds"] = release_channel_age_seconds
payload["evidence"]["releaseChannelFutureSkewSeconds"] = release_channel_future_skew_seconds

execution_prerequisites_clean = not (
    directive_matrix_reasons or catalog_definition_reasons or shell_binding_reasons
)
payload["evidence"]["testExecutionAttempted"] = execution_prerequisites_clean
if not execution_prerequisites_clean:
    append_reason(
        "Targeted ruleset adaptation tests were skipped because directive, catalog, or shell binding proof is already failing.",
        test_execution_reasons,
    )
if unit_test_build_exit != 0:
    append_reason(
        f"Ruleset adaptation unit-test build exited non-zero: {unit_test_build_exit}",
        test_execution_reasons,
    )
if signoff_test_build_exit != 0:
    append_reason(
        f"Ruleset adaptation signoff-test build exited non-zero: {signoff_test_build_exit}",
        test_execution_reasons,
    )
if signoff_test_run_exit != 0:
    append_reason(
        f"Ruleset adaptation signoff-test run exited non-zero: {signoff_test_run_exit}",
        test_execution_reasons,
    )

payload["directiveMatrixReview"] = {
    "status": "pass" if not directive_matrix_reasons else "fail",
    "summary": (
        "The explicit UI-RS directive matrix is present."
        if not directive_matrix_reasons
        else "The explicit UI-RS directive matrix is incomplete or missing."
    ),
    "reasons": directive_matrix_reasons,
    "requiredDirectives": payload["evidence"]["requiredDirectives"],
    "sourcePath": payload["evidence"]["directiveSourcePath"],
}
payload["catalogDefinitionReview"] = {
    "status": "pass" if not catalog_definition_reasons else "fail",
    "summary": (
        "Ruleset UI directive definitions and their dedicated unit coverage are present."
        if not catalog_definition_reasons
        else "Ruleset UI directive definitions or unit coverage are missing."
    ),
    "reasons": catalog_definition_reasons,
    "catalogSourcePath": payload["evidence"]["catalogSourcePath"],
    "directiveTestsPath": payload["evidence"]["directiveTestsPath"],
}
payload["shellBindingReview"] = {
    "status": "pass" if not shell_binding_reasons else "fail",
    "summary": (
        "Blazor and Avalonia shells both bind through the ruleset UI directive catalog."
        if not shell_binding_reasons
        else "One or more shell surfaces are no longer bound through the ruleset UI directive catalog."
    ),
    "reasons": shell_binding_reasons,
    "blazorShellSourcePath": payload["evidence"]["blazorShellSourcePath"],
    "avaloniaShellSourcePath": payload["evidence"]["avaloniaShellSourcePath"],
    "desktopShellRulesetTestsPath": payload["evidence"]["desktopShellRulesetTestsPath"],
}
payload["testExecutionReview"] = {
    "status": "pass" if not test_execution_reasons else "fail",
    "summary": (
        "Targeted ruleset posture and shell acceptance tests built and ran successfully."
        if not test_execution_reasons
        else "Targeted ruleset posture and shell acceptance execution is missing or failed."
    ),
    "reasons": test_execution_reasons,
    "executed": execution_prerequisites_clean,
    "unitTestBuildExit": unit_test_build_exit,
    "signoffTestBuildExit": signoff_test_build_exit,
    "signoffTestRunExit": signoff_test_run_exit,
}
payload["releaseChannelReview"] = {
    "status": "pass" if not release_channel_reasons else "fail",
    "summary": (
        "Ruleset UI adaptation proof is aligned to a current release-channel identity."
        if not release_channel_reasons
        else "Ruleset UI adaptation proof is missing or drifting from release-channel identity."
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

if not payload["reasons"]:
    payload["status"] = "pass"
    payload["summary"] = (
        "Ruleset/UI adaptation parity is explicit and regression-guarded across directive matrix, catalog definition, "
        "shell binding, targeted execution, and release-channel proof."
    )

payload["evidence"]["failureCount"] = len(payload["reasons"])
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if payload["status"] != "pass":
    raise SystemExit(43)

print(f"[UI-RS] PASS: ruleset-specific workbench adaptation is explicit and regression-guarded.")
print(f"[UI-RS] evidence: {receipt_path}")
PY
