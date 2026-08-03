import ast
import re
from pathlib import Path


def test_blazor_container_builder_matches_repo_sdk_pin() -> None:
    global_json = Path("global.json").read_text(encoding="utf-8")
    dockerfile = Path("Chummer.Blazor/Dockerfile").read_text(encoding="utf-8")
    sdk_match = re.search(r'"version"\s*:\s*"([^"]+)"', global_json)

    assert sdk_match, "global.json must declare an SDK version"
    assert f"FROM mcr.microsoft.com/dotnet/sdk:{sdk_match.group(1)} AS build" in dockerfile
    assert "FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build" not in dockerfile
    assert dockerfile.count("-p:ChummerUseLocalCompatibilityTree=true") == 3
    assert dockerfile.count("-p:RestoreAdditionalProjectSources=/chummer-owner-feed") == 3
    assert "Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj" in dockerfile
    assert "Chummer.Play.Contracts/Chummer.Play.Contracts.csproj" in dockerfile
    assert "Chummer.Run.Contracts/Chummer.Run.Contracts.csproj" in dockerfile
    assert "type=cache,id=chummer-nuget-packages" not in dockerfile
    assert dockerfile.count("-p:RestorePackagesPath=/tmp/chummer-nuget-packages") == 5


def test_public_edge_execution_shell_wrapper_uses_alias_safe_repo_root_and_physical_workspace_root() -> None:
    shell = Path("scripts/e2e-public-edge-execution.sh").read_text(encoding="utf-8")

    assert 'SCRIPT_DIR_PHYSICAL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"' in shell
    assert 'REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR_PHYSICAL/.." && pwd -P)"' in shell
    assert 'REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"' in shell
    assert 'REPO_ROOT="$REPO_ROOT_PHYSICAL"' in shell
    assert 'SCRIPT_DIR="$REPO_ROOT/scripts"' in shell
    assert 'WORKSPACE_ROOT="$(cd "$REPO_ROOT_PHYSICAL/.." && pwd -P)"' in shell
    assert 'WORKSPACE_ROOT="$(cd "$REPO_ROOT/.." && pwd)"' not in shell
    assert 'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"' not in shell
    assert 'REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"' not in shell


def _extract_js_array(source: str, name: str) -> set[str]:
    match = re.search(rf"const {re.escape(name)} = \[(.*?)\];", source, re.DOTALL)
    assert match, f"missing JS array {name}"
    return set(re.findall(r"'([^']+)'", match.group(1)))


def _extract_python_set(source: str, name: str) -> set[str]:
    match = re.search(rf"{re.escape(name)} = (\{{.*?\}})", source, re.DOTALL)
    assert match, f"missing Python set {name}"
    parsed = ast.literal_eval(match.group(1))
    return set(parsed)


def test_public_edge_execution_runner_defaults_to_smoke_scope_and_keeps_full_lane_available() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "const playwrightScope = (process.env.CHUMMER_PUBLIC_EDGE_PLAYWRIGHT_SCOPE || 'smoke').trim().toLowerCase();" in script
    assert "const availableWorkflowFamilyIds = [" in script
    assert "const smokeRequiredWorkflowFamilyIds = [" in script
    assert "function normalizePlaywrightScope()" in script
    assert "return playwrightScope === 'full' ? 'full' : 'smoke';" in script
    assert "console.log(`public-edge playwright scope: ${normalizedScope}`);" in script
    assert "if (normalizedScope === 'full') {" in script

    available = _extract_js_array(script, "availableWorkflowFamilyIds")
    smoke = _extract_js_array(script, "smokeRequiredWorkflowFamilyIds")

    assert smoke < available
    assert "promoted_advanced_committed_actions" in available
    assert "promoted_advanced_committed_actions" not in smoke
    assert "promoted_combat_support_execution" in available
    assert "promoted_identity_license_execution" in available
    assert smoke == {
        "promoted_startup_command_executions",
        "promoted_dense_tool_surfaces",
        "promoted_origin_rules_continuity",
        "promoted_build_lab_continuity",
        "promoted_resumed_workspace",
        "promoted_result_continuations",
        "promoted_action_continuations",
        "promoted_committed_actions",
        "promoted_advanced_action_executions",
    }


def test_public_edge_execution_verifier_accepts_scope_specific_required_workflow_sets() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")
    verifier = Path("scripts/verify_blazor_public_edge_execution_proof.py").read_text(encoding="utf-8")

    assert 'SUPPORTED_PLAYWRIGHT_SCOPES = {"smoke", "full"}' in verifier
    assert 'AVAILABLE_WORKFLOW_FAMILY_IDS = {' in verifier
    assert 'FULL_ONLY_WORKFLOW_FAMILY_IDS = {' in verifier
    assert 'SMOKE_REQUIRED_WORKFLOW_FAMILY_IDS = {' in verifier
    assert 'def required_workflow_family_ids_for_scope(scope: str) -> set[str]:' in verifier
    assert 'if scope == "full":' in verifier
    assert 'return set(SMOKE_REQUIRED_WORKFLOW_FAMILY_IDS)' in verifier
    assert 'playwright_scope must be one of' in verifier
    assert 'required_workflow_family_ids mismatch for scope' in verifier
    assert 'missing required workflow families for ' in verifier

    assert _extract_python_set(verifier, "AVAILABLE_WORKFLOW_FAMILY_IDS") == _extract_js_array(
        script,
        "availableWorkflowFamilyIds",
    )
    assert _extract_python_set(verifier, "SMOKE_REQUIRED_WORKFLOW_FAMILY_IDS") == _extract_js_array(
        script,
        "smokeRequiredWorkflowFamilyIds",
    )


def test_public_edge_execution_runner_reuses_rewritten_fixture_workspace() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "let promotedContinuationQuery = 'runner=blue';" in script
    assert "let promotedContinuationQuery = 'workspace=ws-1';" not in script
    assert "function continuationQueryFromUrl(urlString)" in script
    assert "function isReusableContinuationQuery(query)" in script
    assert "return query.startsWith('workspace=') || (query.startsWith('runner=') && query !== 'runner=blue');" in script
    assert "if (isReusableContinuationQuery(promotedContinuationQuery))" in script
    assert "return `workspace=${workspaceId}`;" in script
    assert "return `fixture=${fixtureId}`;" in script
    assert "await openPath(page, `${promotedRouteBase}?fixture=${encodeURIComponent(fixtureId)}`, 'section.classic-chummer-shell');" in script
    assert "new URL(window.location.href).searchParams.has('workspace')" in script
    assert "const rewrittenQuery = continuationQueryFromUrl(page.url());" in script
    assert "if (rewrittenQuery && isReusableContinuationQuery(rewrittenQuery))" in script
    assert "const resolvedQuery = continuationQueryFromUrl(resolvedHref);" in script


def test_public_edge_execution_runner_waits_for_result_continuation_text() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "async function waitForBodyTextIncludes(page, expected, label)" in script
    assert "document.body.innerText || document.body.textContent || ''" in script
    assert "await waitForBodyTextIncludes(page, expectedText, `hosted resumed result route ${route}`);" in script
    assert "const route = `${promotedRouteBase}?fixture=blue&tab=tab-calendar&control=create_entry&dialog_action=add`;" in script
    assert "await waitForBodyTextIncludes(page, \"Entry 'New entry' added.\", 'hosted committed action route');" in script
    assert "await waitForBodyTextIncludes(page, expectedText, `hosted advanced committed action route ${route}`);" in script
    committed_block = script.split("async function auditCommittedAction(page) {", 1)[1].split(
        "async function auditAdvancedCommittedAction(page, route, expectedText) {",
        1,
    )[0]
    advanced_committed_block = script.split(
        "async function auditAdvancedCommittedAction(page, route, expectedText) {",
        1,
    )[1].split("const normalizedScope", 1)[0]
    assert "await openPath(page, route, 'body');" not in committed_block
    assert "await openPath(page, route, 'body');" not in advanced_committed_block


def test_public_edge_execution_runner_preserves_published_receipt_on_live_failure() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "const failedOutputPath = process.env.CHUMMER_PUBLIC_EDGE_FAILED_EXECUTION_PROOF_PATH" in script
    assert "'.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json'" in script
    assert "'BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.failed.generated.json'" in script
    assert "const writeFailedProofToOutput = process.env.CHUMMER_PUBLIC_EDGE_WRITE_FAILED_PROOF_TO_OUTPUT === '1';" in script
    assert "receipt.status === 'passed' || writeFailedProofToOutput ? outputPath : failedOutputPath" in script
    assert "public-edge execution failed; wrote failure receipt" in script
    assert "left published receipt unchanged" in script


def test_public_edge_execution_runner_retries_network_changed_navigation() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "function shouldRetryRouteNavigation(error)" in script
    assert "message.includes('ERR_ABORTED')" in script
    assert "message.includes('ERR_NETWORK_CHANGED')" in script
    assert "message.includes('Timeout')" in script


def test_blazor_route_host_uses_fast_no_prerender_with_visible_workbench_fallback() -> None:
    app = Path("Chummer.Blazor/Components/App.razor").read_text(encoding="utf-8")

    assert '<Routes @rendermode="new InteractiveServerRenderMode(prerender: false)" />' in app
    assert 'data-ssr-workbench-fallback="true"' in app
    assert "BuildWorkbenchFallback()" in app
    assert "window.chummerWorkbenchFallback.removeWhenInteractiveShellAppears" in app
    assert "section.classic-chummer-shell:not([data-ssr-workbench-fallback])" in app
    assert "(\"complex_form_add\", \"add\") => \"Complex form 'Cleaner' added.\"" in app
    assert '"complex_form_add" => new WorkbenchFallbackDialog("Add Complex Form"' in app
    assert '"new_character_origin" => new WorkbenchFallbackDialog("Origin Dossier"' in app
    assert "Pick only the basics, then build the story. Advanced controls are optional." in app
    assert "Create the story first. Review it, then continue to a guided build if you want mechanics." not in app


def test_public_edge_execution_origin_wizard_copy_matches_origin_shell_parity() -> None:
    script = Path("scripts/e2e-public-edge-playwright.cjs").read_text(encoding="utf-8")

    assert "Pick only the basics, then build the story. Advanced controls are optional." in script
    assert "Create the story first. Review it, then continue to a guided build if you want mechanics." not in script


def test_blazor_workbench_shell_publishes_classic_browser_execution_state() -> None:
    preview_markup = Path("Chummer.Blazor/Components/Pages/Preview.razor").read_text(encoding="utf-8")
    shell_markup = Path("Chummer.Blazor/Components/Layout/DesktopShell.razor").read_text(encoding="utf-8")
    shell_code = Path("Chummer.Blazor/Components/Layout/DesktopShell.razor.cs").read_text(encoding="utf-8")

    assert '<section id="chummer-online-app" class="classic-chummer-shell"' in preview_markup
    assert 'data-workbench-committed-result="@CommittedResultDataKey"' in preview_markup
    assert "private static string NormalizeShellDataToken(string? value)" in preview_markup
    assert '(_, "create-entry", "add") => "Entry \'New entry\' added."' in preview_markup
    assert '(_, "edit-entry", "apply") => "Entry renamed to \'Current Entry\'."' in preview_markup
    assert '(_, "delete-entry", "delete") => "Entry \'Current Entry\' removed."' in preview_markup
    assert '(_, "open-notes", "save") => "Notes saved."' in preview_markup
    assert 'data-tab="@TabDataKey"' in preview_markup
    assert 'data-ruleset="@RulesetDataKey"' in preview_markup
    assert 'data-active-workflow="@ActiveWorkflowDataKey"' in preview_markup
    assert 'data-route-segment="@RouteSegmentDataKey"' in preview_markup
    assert 'data-active-runner="@ActiveRunnerDataKey"' in preview_markup
    assert 'data-legacy-runner="@LegacyRunnerDataKey"' in preview_markup
    assert '<section class="desktop-shell classic-desktop-shell @(' in shell_markup
    assert 'inert="@(State.ActiveDialog is not null)"' in shell_markup
    assert 'aria-hidden="@(State.ActiveDialog is not null ? "true" : null)"' in shell_markup
    assert 'classic-desktop-shell classic-chummer-shell' not in shell_markup
    assert 'string.Equals(ClassicShellActiveTabId, "tab-create", StringComparison.Ordinal)' in shell_code
    assert 'return "build-lab";' in shell_code


def test_promoted_workbench_surfaces_startup_command_display_labels() -> None:
    preview = Path("Chummer.Blazor/Components/Pages/Preview.razor").read_text(encoding="utf-8")

    assert 'data-chummer-app-startup-command="@StartupCommandLabel"' in preview
    assert "<strong>@StartupCommandDisplayLabel</strong>" in preview
    assert "private string StartupCommandDisplayLabel" in preview
    assert "return (normalizedCommand, NormalizeShellDataToken(DialogAction)) switch" in preview
    assert '(NewCharacterCommand, _) => "New runner"' in preview
    assert '(NewCharacterOriginCommand, _) => "Origin Dossier"' in preview
    assert '(OpenForPrintingCommand, _) => "Open Print Staging"' in preview
    assert '(OpenForExportCommand, _) => "Open Export Staging"' in preview


def test_workbench_click_targets_keep_native_navigation_fallbacks() -> None:
    preview = Path("Chummer.Blazor/Components/Pages/Preview.razor").read_text(encoding="utf-8")

    assert "private string WorkbenchNewRunnerHref" in preview
    assert 'class="classic-chummer-menu browser-app-classic-menu-bar"' in preview
    assert 'class="classic-menu-item browser-app-classic-menu-root" data-app-menu-root="file"' in preview
    assert '<summary role="button" tabindex="0" aria-expanded="false" data-app-menu-summary="file">File</summary>' in preview
    assert 'href="@WorkbenchNewRunnerHref"' in preview
    assert 'data-app-menu-item="new-runner"' in preview
    assert '@onclick:preventDefault="true"' not in preview
    assert '<a href="@ChummerOnlineRosterHref" aria-current="@(IsCharacterRosterCommand ? "page" : null)">Unsorted local dossier</a>' in preview
    assert 'data-workbench-dock-action="character-roster" aria-current="@(IsCharacterRosterCommand ? "page" : null)"' in preview
    assert 'data-workbench-search-filter-action="roster-search" aria-current="@(IsCharacterRosterCommand ? "page" : null)"' in preview


def test_clickable_surface_e2e_audits_every_unique_interactive_contract() -> None:
    script = Path("scripts/e2e-clickable-surface-playwright.cjs").read_text(encoding="utf-8")

    assert "chummer6-ui.clickable-surface-e2e" in script
    assert "'a[href]'" in script
    assert "'button:not([disabled])'" in script
    assert "'summary'" in script
    assert "'[role=\"button\"]:not([aria-disabled=\"true\"])'" in script
    assert "'[role=\"menuitem\"]:not([aria-disabled=\"true\"])'" in script
    assert "failureKind: passed ? '' : (hrefValid ? 'no_observable_effect' : 'missing_href')" in script
    assert "sameDocumentFragmentAffordance" in script
    assert "element.closest('[inert], [aria-hidden=\"true\"]')" in script
    assert "process.env.CHUMMER_CLICK_AUDIT_LABELS" in script
    assert "process.env.CHUMMER_CLICK_AUDIT_LABELS_JSON" in script
    assert "CHUMMER_CLICK_AUDIT_LABELS_JSON must be a JSON array of strings" in script
    assert "process.env.CHUMMER_PLAYWRIGHT_EXECUTABLE_PATH" in script
    assert "process.env.CHUMMER_CLICK_AUDIT_RETRIES" in script
    assert "data-ssr-workbench-fallback" in script
    assert "const remainedOnSourceDocument = page.url() === before.url;" in script
    assert "uniqueInteractiveContracts" in script
    assert "browserErrorContracts" in script
    assert "collectionFailures.length === 0 && failed.length === 0 && browserErrors.length === 0" in script
