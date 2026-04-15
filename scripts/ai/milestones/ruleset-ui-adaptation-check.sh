#!/usr/bin/env bash
set -euo pipefail

echo "[UI-RS] checking ruleset-specific workbench adaptation guardrails..."

require_contains() {
  local path="$1"
  local pattern="$2"
  local message="$3"
  if ! rg -q "$pattern" "$path"; then
    echo "$message"
    exit 3
  fi
}

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

echo "[UI-RS] executing targeted ruleset posture and shell acceptance tests..."
scripts/ai/with-package-plane.sh build Chummer.Tests/Chummer.Tests.csproj --nologo --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false
scripts/ai/with-package-plane.sh build Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --nologo --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false
scripts/ai/with-package-plane.sh run --project Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --no-build --nologo --verbosity quiet

echo "[UI-RS] PASS: ruleset-specific workbench adaptation is explicit and regression-guarded."
